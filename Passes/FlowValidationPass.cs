using System;
using System.Collections.Generic;

public class FlowValidationPass : DefaultVisitor
{
	private class State
	{
		public bool didReturn;
		public bool warnedDeadCode;
		public Dictionary<Symbol, List<Location>> usesBeforeDefinition = new Dictionary<Symbol, List<Location>>();
		
		public State Clone()
		{
			return new State {
				didReturn = didReturn,
				warnedDeadCode = warnedDeadCode,
				usesBeforeDefinition = usesBeforeDefinition,
			};
		}
	}
	
	private Log log;
	private Stack<State> stack;
	
	public FlowValidationPass(Log log)
	{
		this.log = log;
	}
	
	private State Push()
	{
		State state = stack.Peek().Clone();
		stack.Push(state);
		return state;
	}
	
	public override Null Visit(ExternalStmt node)
	{
		// Control flow isn't possible in external blocks
		return null;
	}
	
	public override Null Visit(FuncDef node)
	{
		// Follow control flow through the function
		stack = new Stack<State>();
		stack.Push(new State());
		base.Visit(node);
		
		// Make sure all control paths return a value
		if (!(node.symbol.type.ReturnType() is VoidType) && !stack.Peek().didReturn) {
			log.ErrorNotAllPathsReturnValue(node.location);
		}
		
		stack = null;
		return null;
	}
	
	public override Null Visit(VarDef node)
	{
		if (stack != null) {
			// Check for variable usage in the initializer
			base.Visit(node);
			
			// Error if a variable is defined after its use
			Dictionary<Symbol, List<Location>> map = stack.Peek().usesBeforeDefinition;
			List<Location> locations;
			if (map.TryGetValue(node.symbol, out locations)) {
				foreach (Location location in locations) {
					log.ErrorUseBeforeDefinition(location, node.symbol.def.name);
				}
			}
		}
		
		return null;
	}
	
	public override Null Visit(IdentExpr node)
	{
		// Mark uses of new symbols for error reporting
		Dictionary<Symbol, List<Location>> map = stack.Peek().usesBeforeDefinition;
		List<Location> locations;
		if (!map.TryGetValue(node.symbol, out locations)) {
			map[node.symbol] = locations = new List<Location>();
		}
		locations.Add(node.location);
		
		return null;
	}
	
	public override Null Visit(Block node)
	{
		// Warn for statements after control flow has ended
		foreach (Stmt stmt in node.stmts) {
			if (stack != null && stack.Peek().didReturn && !stack.Peek().warnedDeadCode) {
				log.WarningDeadCode(stmt.location);
				stack.Peek().warnedDeadCode = true;
			}
			stmt.Accept(this);
		}
		
		return null;
	}
	
	public override Null Visit(ReturnStmt node)
	{
		base.Visit(node);
		stack.Peek().didReturn = true;
		return null;
	}
	
	public override Null Visit(IfStmt node)
	{
		bool thenReturn = false;
		bool elseReturn = false;
		node.test.Accept(this);
		
		// Follow the true branch
		Push();
		node.thenBlock.Accept(this);
		thenReturn = stack.Peek().didReturn;
		stack.Pop();
		
		// Follow the false branch
		if (node.elseBlock != null) {
			Push();
			node.elseBlock.Accept(this);
			elseReturn = stack.Peek().didReturn;
			stack.Pop();
		}
		
		// Stop control flow here if it stopped for both branches
		if (thenReturn && elseReturn) {
			stack.Peek().didReturn = true;
		}
		
		return null;
	}
}
