using System;
using System.Collections.Generic;

public class JsTarget
{
	public static string Generate(Module module)
	{
		return module.Accept(new JsTargetVisitor());
	}
}

public class JsTargetVisitor : Visitor<string>
{
	private static readonly Dictionary<UnaryOp, string> unaryOpToString = new Dictionary<UnaryOp, string> {
		{ UnaryOp.Negative, "-" },
		{ UnaryOp.Not, "!" },
	};
	
	private static readonly Dictionary<BinaryOp, string> binaryOpToString = new Dictionary<BinaryOp, string> {
		{ BinaryOp.Assign, "=" },
		
		{ BinaryOp.And, "&&" },
		{ BinaryOp.Or, "||" },
		
		{ BinaryOp.Add, "+" },
		{ BinaryOp.Subtract, "-" },
		{ BinaryOp.Multiply, "*" },
		{ BinaryOp.Divide, "/" },
		
		{ BinaryOp.Equal, "==" },
		{ BinaryOp.NotEqual, "!=" },
		{ BinaryOp.LessThan, "<" },
		{ BinaryOp.GreaterThan, ">" },
		{ BinaryOp.LessThanEqual, "<=" },
		{ BinaryOp.GreaterThanEqual, ">=" },
	};
	
	private static readonly Dictionary<BinaryOp, int> jsBinaryOpPrecedence = new Dictionary<BinaryOp, int> {
		// From https://developer.mozilla.org/en/JavaScript/Reference/Operators/Operator_Precedence
		{ BinaryOp.Multiply, 5 },
		{ BinaryOp.Divide, 5 },
		{ BinaryOp.Add, 6 },
		{ BinaryOp.Subtract, 6 },
		{ BinaryOp.LessThan, 8 },
		{ BinaryOp.LessThanEqual, 8 },
		{ BinaryOp.GreaterThan, 8 },
		{ BinaryOp.GreaterThanEqual, 8 },
		{ BinaryOp.Equal, 9 },
		{ BinaryOp.NotEqual, 9 },
		{ BinaryOp.And, 13 },
		{ BinaryOp.Or, 14 },
		{ BinaryOp.Assign, 16 },
	};
	
	private string indent = "";
	
	private void Indent()
	{
		indent += "  ";
	}
	
	private void Dedent()
	{
		indent = indent.Substring(2);
	}
	
	public override string Visit(Block node)
	{
		string text = "{\n";
		Indent();
		text += string.Join("", node.stmts.ConvertAll(x => x.Accept(this)).ToArray());
		Dedent();
		return text + indent + "}";
	}

	public override string Visit(Module node)
	{
		return string.Join("", node.block.stmts.ConvertAll(x => x.Accept(this)).ToArray());
	}

	public override string Visit(IfStmt node)
	{
		return indent + "if (" + node.test.Accept(this) + ") " + node.thenBlock.Accept(this) + (
			node.elseBlock == null ? "" : " else " + node.elseBlock.Accept(this)) + "\n";
	}

	public override string Visit(ReturnStmt node)
	{
		return indent + "return" + (node.value == null ? "" : " " + node.value.Accept(this).StripParens()) + ";\n";
	}

	public override string Visit(ExprStmt node)
	{
		return indent + node.value.Accept(this) + ";\n";
	}
	
	public override string Visit(ExternalStmt node)
	{
		return "";
	}
	
	public override string Visit(WhileStmt node)
	{
		return indent + "while (" + node.test.Accept(this) + ") " + node.block.Accept(this) + "\n";
	}
	
	public override string Visit(VarDef node)
	{
		return indent + "var " + node.name + (node.value == null ? "" : " = " + node.value.Accept(this).StripParens()) + ";\n";
	}

	public override string Visit(FuncDef node)
	{
		return indent + "function " + node.name + "(" + string.Join(", ", node.argDefs.ConvertAll(x => x.name).ToArray()) +
			") " + node.block.Accept(this) + "\n";
	}
	
	private string PrintMembers(string prefix, List<Stmt> stmts)
	{
		string text = "";
		foreach (Stmt stmt in stmts) {
			if (stmt is ClassDef) {
				ClassDef def = (ClassDef)stmt;
				text += prefix + "." + def.name + " = function() {};\n";
				text += PrintMembers(prefix + "." + def.name, def.block.stmts);
			} else if (stmt is VarDef) {
				VarDef def = (VarDef)stmt;
				text += prefix + ".prototype." + def.name + " = " + (def.value == null ? "null" : def.value.Accept(this)) + ";\n";
			} else if (stmt is FuncDef) {
				FuncDef def = (FuncDef)stmt;
				text += prefix + (def.isStatic ? "." : ".prototype.") + def.name + " = function(" + string.Join(", ",
					def.argDefs.ConvertAll(x => x.name).ToArray()) + ") " + def.block.Accept(this) + ";\n";
			} else {
				text += stmt.Accept(this);
			}
		}
		return text;
	}
	
	public override string Visit(ClassDef node)
	{
		return "function " + node.name + "() {}\n" + PrintMembers(node.name, node.block.stmts);
	}
	
	public override string Visit(NullExpr node)
	{
		return "null";
	}

	public override string Visit(ThisExpr node)
	{
		return "this";
	}

	public override string Visit(BoolExpr node)
	{
		return node.value ? "true" : "false";
	}
	
	public override string Visit(IntExpr node)
	{
		return node.value.ToString();
	}

	public override string Visit(FloatExpr node)
	{
		return node.value.ToString();
	}

	public override string Visit(StringExpr node)
	{
		return node.value.ToQuotedString();
	}
	
	public override string Visit(IdentExpr node)
	{
		return node.symbol.def.name;
	}
	
	public override string Visit(TypeExpr node)
	{
		return "/* " + node.type + " */";
	}
	
	public override string Visit(UnaryExpr node)
	{
		return "(" + unaryOpToString[node.op] + node.value.Accept(this) + ")";
	}
	
	public override string Visit(BinaryExpr node)
	{
		// Strip parentheses if they aren't needed
		string left = node.left.Accept(this);
		string right = node.right.Accept(this);
		if (node.left is BinaryExpr && jsBinaryOpPrecedence[node.op] >= jsBinaryOpPrecedence[((BinaryExpr)node.left).op]) {
			left = left.StripParens();
		}
		if (node.right is BinaryExpr && jsBinaryOpPrecedence[node.op] >= jsBinaryOpPrecedence[((BinaryExpr)node.right).op]) {
			right = right.StripParens();
		}
		return "(" + left + " " + binaryOpToString[node.op] + " " + right + ")";
	}

	public override string Visit(CallExpr node)
	{
		return node.func.Accept(this) + "(" + string.Join(", ", node.args.ConvertAll(x => x.Accept(this).StripParens()).ToArray()) + ")";
	}
	
	public override string Visit(CastExpr node)
	{
		if (node.value.computedType.IsFloat() && ((MetaType)node.target.computedType).instanceType.IsInt()) {
			return "(" + node.value.Accept(this) + " | 0)";
		}
		return node.value.Accept(this);
	}
	
	public override string Visit(MemberExpr node)
	{
		return node.obj.Accept(this) + "." + node.symbol.def.name;
	}
}
