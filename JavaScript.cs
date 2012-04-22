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
		
		{ BinaryOp.LShift, "<<" },
		{ BinaryOp.RShift, ">>" },
		
		{ BinaryOp.Equal, "==" },
		{ BinaryOp.NotEqual, "!=" },
		{ BinaryOp.LessThan, "<" },
		{ BinaryOp.GreaterThan, ">" },
		{ BinaryOp.LessThanEqual, "<=" },
		{ BinaryOp.GreaterThanEqual, ">=" },
	};
	// From https://developer.mozilla.org/en/JavaScript/Reference/Operators/Operator_Precedence
	private static readonly Dictionary<BinaryOp, int> jsBinaryOpPrecedence = new Dictionary<BinaryOp, int> {
		{ BinaryOp.Multiply, 5 },
		{ BinaryOp.Divide, 5 },
		{ BinaryOp.Add, 6 },
		{ BinaryOp.Subtract, 6 },
		{ BinaryOp.LShift, 7 },
		{ BinaryOp.RShift, 7 },
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
	private string prefix = "";
	
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
		string oldPrefix = prefix;
		prefix = "";
		text += node.stmts.ConvertAll(x => x.Accept(this)).Join();
		prefix = oldPrefix;
		Dedent();
		return text + indent + "}";
	}

	public override string Visit(Module node)
	{
		return "\"use strict\";\n" + node.block.stmts.ConvertAll(x => x.Accept(this)).Join();
	}

	public override string Visit(IfStmt node)
	{
		return indent + "if (" + node.test.Accept(this).StripParens() + ") " + node.thenBlock.Accept(this) + (
			node.elseBlock == null ? "" : " else " + node.elseBlock.Accept(this)) + "\n";
	}

	public override string Visit(ReturnStmt node)
	{
		return indent + "return" + (node.value == null ? "" : " " + node.value.Accept(this).StripParens()) + ";\n";
	}

	public override string Visit(ExprStmt node)
	{
		return indent + node.value.Accept(this).StripParens() + ";\n";
	}
	
	public override string Visit(ExternalStmt node)
	{
		return "";
	}
	
	public override string Visit(WhileStmt node)
	{
		return indent + "while (" + node.test.Accept(this).StripParens() + ") " + node.block.Accept(this) + "\n";
	}
	
	private string DefineVar(Def node)
	{
		return (prefix.Length > 0 ? prefix : "var ") + node.name;
	}
	
	public override string Visit(VarDef node)
	{
		return indent + DefineVar(node) + (node.value == null ? "" : " = " + node.value.Accept(this).StripParens()) + ";\n";
	}

	public override string Visit(FuncDef node)
	{
		return indent + DefineVar(node) + " = function(" + node.argDefs.ConvertAll(x => x.name).Join(", ") +
			") " + node.block.Accept(this) + ";\n";
	}
	
	public override string Visit(ClassDef node)
	{
		// Write out the class constructor
		string text = indent + DefineVar(node) + " = function() {\n";
		Indent();
		foreach (Stmt stmt in node.block.stmts) {
			if (stmt is VarDef) {
				VarDef varDef = (VarDef)stmt;
				text += indent + "this." + varDef.name + " = " + (varDef.value == null ? "null" : varDef.value.Accept(this).StripParens()) + ";\n";
			}
		}
		Dedent();
		text += indent + "};\n";
		
		// Write out members
		string oldPrefix = prefix;
		foreach (Stmt stmt in node.block.stmts) {
			if (!(stmt is VarDef)) {
				bool isStatic = (!(stmt is FuncDef) || ((FuncDef)stmt).isStatic);
				prefix = oldPrefix + node.name + (isStatic ? "." : ".prototype.");
				text += stmt.Accept(this);
			}
		}
		prefix = oldPrefix;
		return text;
	}
	
	public override string Visit(VarExpr node)
	{
		throw new NotImplementedException();
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
		throw new NotImplementedException();
	}
	
	public override string Visit(ListExpr node)
	{
		return "[" + node.items.ConvertAll(x => x.Accept(this).StripParens()).Join(", ") + "]";
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
		return (node.isCtor ? "new " : "") + node.func.Accept(this) + "(" +
			node.args.ConvertAll(x => x.Accept(this).StripParens()).Join(", ") + ")";
	}
	
	public override string Visit(ParamExpr node)
	{
		throw new NotImplementedException();
	}
	
	public override string Visit(CastExpr node)
	{
		if (node.value.computedType.IsFloat() && node.target.computedType.InstanceType().IsInt()) {
			return "(" + node.value.Accept(this) + " | 0)";
		}
		return node.value.Accept(this);
	}
	
	public override string Visit(MemberExpr node)
	{
		return node.obj.Accept(this) + "." + node.symbol.def.name;
	}
	
	public override string Visit(IndexExpr node)
	{
		return node.obj.Accept(this) + "[" + node.index.Accept(this) + "]";
	}
}
