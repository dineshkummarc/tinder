using System;
using System.IO;
using System.Collections.Generic;

// This automatically writes all graphs in the compiled program to "graph.dot"
// for debugging. To visualize the graph, run the following command:
//
//     dot bin/Debug/graphs.dot -Tpng -ograph.png; open graph.png
//
public class FlowValidationPass : DefaultVisitor
{
	private readonly Log log;
	private readonly Dictionary<FlowNode, Knowledge> knowledgeCache = new Dictionary<FlowNode, Knowledge>();
	private Dictionary<Symbol, List<SSANode>> ssaNodesForSymbol = new Dictionary<Symbol, List<SSANode>>();
	private FlowNode rootNode;
	private Pair currentPair;
	private bool inFunc;
	private int nextID;

	// Used to saving DOT flow graphs for debugging
	private HashSet<FlowNode> currentNodes;
	private string graphText;
	
	public FlowValidationPass(Log log)
	{
		this.log = log;
	}

	public override Null Visit(Module node)
	{
		base.Visit(node);
		File.WriteAllText("graphs.dot", "digraph {\n" + graphText + "}\n");
		return null;
	}

	public override Null Visit(Block node)
	{
		// Don't do analysis outside functions
		if (!inFunc) {
			return base.Visit(node);
		}

		// Check for dead code
		Scope old = scope;
		scope = node.scope;
		foreach (Stmt stmt in node.stmts) {
			if (KnowledgeForNode(currentPair.trueNode) == null) {
				log.WarningDeadCode(stmt.location);
				break;
			}
			stmt.Accept(this);
		}
		scope = old;

		return null;
	}

	public override Null Visit(ExternalStmt node)
	{
		// There is no flow to validate in external blocks
		return null;
	}
	
	public override Null Visit(FuncDef node)
	{
		// Prepare for flow analysis
		inFunc = true;
		knowledgeCache.Clear();
		currentNodes = new HashSet<FlowNode>();
		rootNode = Record(new RootNode());
		currentPair = new Pair(rootNode);

		// Perform flow analysis
		base.Visit(node);

		// Check for a return value
		if (!(node.symbol.type.ReturnType() is VoidType) && KnowledgeForNode(currentPair.trueNode) != null) {
			log.ErrorNotAllPathsReturnValue(node.location);
		}

		// Log the generated graph (for debugging)
		graphText += ToDotGraph();

		inFunc = false;
		return null;
	}

	public override Null Visit(VarDef node)
	{
		// Don't do analysis outside functions
		if (!inFunc) {
			return base.Visit(node);
		}

		// Create a new SSA definition that is unique to this node
		SSANode ssaNode = Record(new SSANode(node.symbol, nextID++, currentPair.trueNode));
		ssaNodesForSymbol[node.symbol] = new List<SSANode> { ssaNode };
		currentPair = new Pair(ssaNode);

		// Update the knowledge based on the initial value, if any
		IsNull isNull = (node.value != null) ? IsNullFromExpr(node.value) : IsNullFromType(node.symbol.type);
		currentPair = new Pair(Record(new NullableNode(ssaNode, isNull, currentPair.trueNode)));

		return null;
	}

	public override Null Visit(UnaryExpr node)
	{
		// Don't do analysis outside functions
		if (!inFunc) {
			return base.Visit(node);
		}

		base.Visit(node);

		// Invert the flow for boolean not
		if (node.op == UnaryOp.Not) {
			currentPair = currentPair.Inverse();
		}

		return null;
	}

	public override Null Visit(BinaryExpr node)
	{
		// Don't do analysis outside functions
		if (!inFunc) {
			return base.Visit(node);
		}

		if (node.op == BinaryOp.And) {
			// Calculate flow for the left expression
			node.left.Accept(this);
			Pair leftPair = currentPair;
			
			// Calculate flow for the right expression assuming the left condition was executed
			node.right.Accept(this);
			Pair rightPair = currentPair;
			
			// Join the flow of both false branches
			FlowNode joinNode = Join(leftPair.falseNode, rightPair.falseNode);
			currentPair = new Pair(rightPair.trueNode, joinNode);
		} else if (node.op == BinaryOp.Or) {
			// Calculate flow for the left expression
			node.left.Accept(this);
			Pair leftPair = currentPair;
			
			// Calculate flow for the right expression assuming the left condition wasn't executed
			currentPair = leftPair.Inverse();
			node.right.Accept(this);
			Pair rightPair = currentPair;
			
			// Join the flow of both true branches
			FlowNode joinNode = Join(leftPair.trueNode, rightPair.trueNode);
			currentPair = new Pair(joinNode, rightPair.falseNode);
		} else if (node.op == BinaryOp.Equal || node.op == BinaryOp.NotEqual) {
			base.Visit(node);
			
			// Find the function-local symbol being compared with null, if any
			IdentExpr identExpr;
			if (node.left is IdentExpr && node.right is CastExpr && ((CastExpr)node.right).value is NullExpr) {
				identExpr = (IdentExpr)node.left;
			} else if (node.right is IdentExpr && node.left is CastExpr && ((CastExpr)node.left).value is NullExpr) {
				identExpr = (IdentExpr)node.right;
			} else {
				return null;
			}
			if (identExpr.symbol.def.info.funcDef != null) {
				// Split the flow into null and non-null paths
				FlowNode isNullNode = currentPair.trueNode;
				FlowNode isNotNullNode = currentPair.trueNode;
				foreach (SSANode ssaNode in ssaNodesForSymbol[identExpr.symbol]) {
					isNullNode = Record(new NullableNode(ssaNode, IsNull.Yes, isNullNode));
					isNotNullNode = Record(new NullableNode(ssaNode, IsNull.No, isNotNullNode));
				}

				// Set up true and false branches
				if (node.op == BinaryOp.Equal) {
					currentPair = new Pair(isNullNode, isNotNullNode);
				} else {
					currentPair = new Pair(isNotNullNode, isNullNode);
				}
			}
		} else if (node.op == BinaryOp.Assign) {
			base.Visit(node);

			// Check for assignment to a local variable
			if (node.left is IdentExpr) {
				IdentExpr identExpr = (IdentExpr)node.left;
				if (identExpr.symbol.def.info.funcDef != null) {
					// Create a new SSA node for this assignment
					SSANode ssaNode = Record(new SSANode(identExpr.symbol, nextID++, currentPair.trueNode));
					ssaNodesForSymbol[identExpr.symbol] = new List<SSANode> { ssaNode };
					currentPair = new Pair(ssaNode);

					// Record narrowing information based on the new value
					IsNull isNull = IsNullFromExpr(node.right);
					currentPair = new Pair(Record(new NullableNode(ssaNode, isNull, currentPair.trueNode)));
				}
			}
		} else {
			base.Visit(node);
		}

		return null;
	}

	public override Null Visit(ReturnStmt node)
	{
		// Don't do analysis outside functions
		if (!inFunc) {
			return base.Visit(node);
		}

		base.Visit(node);

		// Return statements stop flow completely
		currentPair = new Pair(null);

		return null;
	}

	public override Null Visit(IfStmt node)
	{
		// Don't do analysis outside functions
		if (!inFunc) {
			return base.Visit(node);
		}

		// Calculate flow for the test expression
		node.test.Accept(this);
		Pair testPair = currentPair;

		// Propagate true flow down the then branch
		currentPair = new Pair(testPair.trueNode);
		node.thenBlock.Accept(this);
		Pair thenPair = currentPair;
		
		// Propagate false flow down the else branch
		currentPair = new Pair(testPair.falseNode);
		if (node.elseBlock != null) {
			node.elseBlock.Accept(this);
		}
		Pair elsePair = currentPair;
		
		// Unify flow from the two branches
		FlowNode joinNode = Join(thenPair.trueNode, elsePair.trueNode);
		currentPair = new Pair(joinNode);

		return null;
	}

	public override Null Visit(CastExpr node)
	{
		base.Visit(node);

		// Check for provably invalid dereferences
		if (node.value is IdentExpr) {
			IdentExpr identExpr = (IdentExpr)node.value;
			List<SSANode> ssaNodes;
			if (ssaNodesForSymbol.TryGetValue(identExpr.symbol, out ssaNodes)) {
				// Find the union over all SSA nodes for this symbol
				Knowledge knowledge = KnowledgeForNode(currentPair.trueNode);
				IsNull union = IsNull.Unknown;
				foreach (SSANode ssaNode in ssaNodes) {
					if (ssaNode.symbol == identExpr.symbol) {
						union |= knowledge.isNull.GetOrDefault(ssaNode, IsNull.Unknown);
					}
				}

				// Take action based on knowledge
				if (union == IsNull.Yes) {
					log.ErrorNullDereference(node.location, identExpr.name);
				} else if (union == IsNull.Maybe) {
					log.WarningNullableDereference(node.location, identExpr.name);
				}
			}
		}

		return null;
	}

	private static IsNull IsNullFromExpr(Expr node)
	{
		if (node is CastExpr) {
			return IsNullFromExpr(((CastExpr)node).value);
		} else {
			return IsNullFromType(node.computedType);
		}
	}

	private static IsNull IsNullFromType(Type type)
	{
		if (type is NullableType) {
			return IsNull.Maybe;
		} else if (type is NullType) {
			return IsNull.Yes;
		} else {
			return IsNull.No;
		}
	}

	private Knowledge KnowledgeForNode(FlowNode node)
	{
		// Compute all knowledge for the current flow node, and cache it for
		// future use inside the same flow node. Note that we can't reuse flow
		// results in the cache in the middle of the computation because the
		// knowledge of a child node cannot be derived from the knowledge of
		// the parent node. The direction of information is in fact the reverse,
		// from child to parent.
		Knowledge knowledge = null;
		if (node != null && !knowledgeCache.TryGetValue(node, out knowledge)) {
			knowledge = knowledgeCache[node] = node.ComputeKnowledge(rootNode);
		}
		return knowledge;
	}
	
	private FlowNode Join(FlowNode left, FlowNode right)
	{
		if (left == null || left == right) {
			return right;
		} else if (right == null) {
			return left;
		} else {
			return Record(new JoinNode(left, right));
		}
	}

	private T Record<T>(T node) where T : FlowNode
	{
		currentNodes.Add(node);
		return node;
	}

	private string ToDotGraph()
	{
		Dictionary<FlowNode, int> ids = new Dictionary<FlowNode, int>();
		string text = "";
		foreach (FlowNode node in currentNodes) {
			int id = ids[node] = nextID++;
			string label;
			if (node is RootNode) {
				label = "root";
			} else if (node is JoinNode) {
				label = "join";
			} else if (node is SSANode) {
				label = "ssa " + (SSANode)node;
			} else if (node is NullableNode) {
				label = ((NullableNode)node).ssaNode + " is " + ((NullableNode)node).isNull.AsString();
			} else {
				continue;
			}
			Knowledge knowledge = KnowledgeForNode(node);
			label += "\nknowledge: " + (knowledge != null ? knowledge.ToString() : "impossible");
			text += "  n" + id + " [label = " + label.ToQuotedString() + "];\n";
		}
		foreach (FlowNode node in currentNodes) {
			foreach (FlowNode parent in node.parents) {
				text += "  n" + ids[parent] + " -> n" + ids[node] + ";\n";
			}
		}
		return text;
	}

	// A tuple of two FlowNode instances, used to handle building graphs from
	// short-circuit "and" and "or" operators. It is always the case that
	// trueNode == falseNode during normal control flow.
	private class Pair
	{
		public readonly FlowNode trueNode;
		public readonly FlowNode falseNode;

		public Pair(FlowNode node)
		{
			trueNode = falseNode = node;
		}

		public Pair(FlowNode trueNode, FlowNode falseNode)
		{
			this.trueNode = trueNode;
			this.falseNode = falseNode;
		}

		public Pair Inverse()
		{
			return new Pair(falseNode, trueNode);
		}
	}

	// A node in the CFG that would be built if the AST were compiled to basic
	// blocks. This graph contains additional nodes involving flow, such
	// as the Nullable node which restricts the type of variables.
	private abstract class FlowNode
	{
		public readonly List<FlowNode> parents;
	
		public FlowNode(List<FlowNode> parents)
		{
			this.parents = parents;
		}

		public virtual Knowledge AddTo(Knowledge knowledge)
		{
			return knowledge;
		}

		public Knowledge ComputeKnowledge(FlowNode rootNode)
		{
			// List the nodes in reverse postorder so that when we visit a node we
			// can be sure that its predecessors were visited first. Also record
			// the predecessors for each node. These are not precomputed and stored
			// because any node can be part of many different supergraphs and will
			// potentially have a different set of previous nodes for each one.
			Dictionary<FlowNode, List<FlowNode>> childNodesForNode = new Dictionary<FlowNode, List<FlowNode>>();
			HashSet<FlowNode> visited = new HashSet<FlowNode>();
			List<FlowNode> postOrder = new List<FlowNode>();
			Action<FlowNode> computePostOrder = (FlowNode node) => {
				if (!visited.Contains(node)) {
					visited.Add(node);
					foreach (FlowNode parent in node.parents) {
						childNodesForNode.GetOrCreate(parent).Add(node);
						computePostOrder(parent);
					}
					postOrder.Add(node);
				}
			};
			computePostOrder(this);
			
			// Propagate knowledge from the start node to the root node
			Dictionary<FlowNode, Knowledge> knowledgeForNode = new Dictionary<FlowNode, Knowledge>();
			for (int i = postOrder.Count - 1; i >= 0; i--) {
				FlowNode node = postOrder[i];
	
				// Merge the information from previous nodes
				Knowledge knowledge;
				List<FlowNode> childNodes = childNodesForNode.GetOrCreate(node);
				List<Knowledge> childKnowledge = childNodes.ConvertAll(n => knowledgeForNode[n]).FindAll(n => n != null);
				if (childKnowledge.Count > 0) {
					// The knowledge is the union over all child knowledge
					knowledge = Knowledge.Union(childKnowledge);
				} else if (childNodes.Count > 0) {
					// All previous nodes have no flow, continuation is impossible
					knowledge = null;
				} else {
					// This is the first node and we are starting from zero knowledge
					knowledge = new Knowledge();
				}
				
				// Add the knowledge contained in this node
				if (knowledge != null) {
					knowledge = node.AddTo(knowledge);
				}
				
				// Propagate knowledge all the way to the root node
				if (node == rootNode) {
					return knowledge;
				}
				
				// Store the flow out of this node, if any
				knowledgeForNode[node] = knowledge;
			}

			return null;
		}
	}
	
	private class RootNode : FlowNode
	{
		public RootNode() : base(new List<FlowNode>())
		{
		}
	}
	
	private class JoinNode : FlowNode
	{
		public JoinNode(FlowNode left, FlowNode right) : base(new List<FlowNode> { left, right })
		{
		}
	}
	
	private class SSANode : FlowNode
	{
		public readonly Symbol symbol;
		public readonly int id;
	
		public SSANode(Symbol symbol, int id, FlowNode parent) : base(new List<FlowNode> { parent })
		{
			this.symbol = symbol;
			this.id = id;
		}

		public override string ToString()
		{
			return symbol.def.name + id;
		}
	}

	private class NullableNode : FlowNode
	{
		public readonly SSANode ssaNode;
		public readonly IsNull isNull;
	
		public NullableNode(SSANode ssaNode, IsNull isNull, FlowNode parent) : base(new List<FlowNode> { parent })
		{
			this.ssaNode = ssaNode;
			this.isNull = isNull;
		}

		public override Knowledge AddTo(Knowledge knowledge)
		{
			// Narrow to more specific knowledge
			IsNull intersection = knowledge.isNull.GetOrDefault(ssaNode, IsNull.Maybe);
			intersection &= isNull;
			knowledge.isNull[ssaNode] = intersection;

			// A contradiction means flow out of this node is impossible
			return (intersection == IsNull.Unknown) ? null : knowledge;
		}
	}

	// Holds all knowledge about a set of SSA variables. Knowledge through an
	// impossible flow path is represented by null.
	private class Knowledge
	{
		public readonly Dictionary<SSANode, IsNull> isNull = new Dictionary<SSANode, IsNull>();

		public static Knowledge Union(List<Knowledge> allKnowledge)
		{
			HashSet<SSANode> ssaNodes = new HashSet<SSANode>();
			Knowledge result = new Knowledge();
			foreach (Knowledge knowledge in allKnowledge) {
				ssaNodes.AddRange(knowledge.isNull.Keys);
			}
			foreach (SSANode ssaNode in ssaNodes) {
				IsNull union = IsNull.Unknown;
				foreach (Knowledge knowledge in allKnowledge) {
					union |= knowledge.isNull.GetOrDefault(ssaNode, IsNull.Unknown);
				}
				result.isNull[ssaNode] = union;
			}
			return result;
		}

		public override string ToString()
		{
			return string.Join(", ", isNull.Items().ConvertAll(pair => {
				return pair.Key + " is " + pair.Value.AsString();
			}).ToArray());
		}
	}
}

// Constructed so that intersection is "&" and union is "|"
public enum IsNull
{
	No = 1,
	Yes = 2,
	Maybe = 3,
	Unknown = 0,
}
