using System;
using System.Collections.Generic;

////////////////////////////////////////////////////////////////////////////////
// ErrorMessages
////////////////////////////////////////////////////////////////////////////////

public static class ErrorMessages
{
	public static void ErrorStmtNotAllowed(this Log log, Location location, string statement, string place)
	{
		log.Error(location, statement + " is not allowed " + place);
	}
	
	public static void ErrorDefaultArgNotAllowed(this Log log, Location location)
	{
		log.Error(location, "functions cannot have default arguments");
	}
	
	public static void ErrorFunctionBody(this Log log, Location location, bool inExternal)
	{
		if (inExternal) {
			log.Error(location, "functions inside external blocks cannot have implementations");
		} else {
			log.Error(location, "functions outside external blocks must have implementations");
		}
	}
	
	public static void ErrorUndefinedSymbol(this Log log, Location location, string name)
	{
		log.Error(location, "reference to undefined symbol \"" + name + "\"");
	}
	
	public static void ErrorNoTypeContext(this Log log, Location location, string name)
	{
		log.Error(location, "cannot deduce the type of " + name + " from the surrounding context");
	}
	
	public static void ErrorNotType(this Log log, Location location, Type type)
	{
		log.Error(location, "value of type \"" + type + "\" is not a type");
	}
	
	public static void ErrorTypeMismatch(this Log log, Location location, Type expected, Type found)
	{
		log.Error(location, "expected value of type \"" + expected + "\" but found value of type \"" + found + "\"");
	}
}

////////////////////////////////////////////////////////////////////////////////
// StructuralCheckPass
////////////////////////////////////////////////////////////////////////////////

public class StructuralCheckPass : DefaultVisitor
{
	public class State
	{
		public bool inClass;
		public bool inExternal;
		public bool inFunction;
		
		public State Reset()
		{
			// Don't clear inExternal because we always want to know that
			inClass = false;
			inFunction = false;
			return this;
		}
		
		public State Clone()
		{
			return new State {
				inClass = inClass,
				inExternal = inExternal,
				inFunction = inFunction
			};
		}
	}
	
	public Log log;
	public Stack<State> stack;
	
	public StructuralCheckPass(Log log)
	{
		this.log = log;
		stack = new Stack<State>();
		stack.Push(new State());
	}
	
	public string NameForStmt(Stmt stmt)
	{
		if (stmt is IfStmt) {
			return "if statement";
		}
		if (stmt is ReturnStmt) {
			return "return";
		}
		if (stmt is ExprStmt) {
			return "free expression";
		}
		if (stmt is ExternalStmt) {
			return "external block";
		}
		if (stmt is VarDef) {
			return "variable";
		}
		if (stmt is FuncDef) {
			return "function";
		}
		if (stmt is ClassDef) {
			return "class";
		}
		return "statement";
	}
	
	public State Push()
	{
		State state = stack.Peek().Clone();
		stack.Push(state);
		return state;
	}
	
	public override Null Visit(Block node)
	{
		if (stack.Peek().inClass) {
			foreach (Stmt stmt in node.stmts) {
				if (stmt is ClassDef || stmt is VarDef || stmt is FuncDef) {
					continue;
				}
				log.ErrorStmtNotAllowed(stmt.location, NameForStmt(stmt), "inside a class definition");
			}
		} else if (stack.Peek().inFunction) {
			foreach (Stmt stmt in node.stmts) {
				if (stmt is VarDef || stmt is ExprStmt || stmt is IfStmt || stmt is ReturnStmt) {
					continue;
				}
				log.ErrorStmtNotAllowed(stmt.location, NameForStmt(stmt), "inside a function body");
			}
		} else if (stack.Peek().inExternal) {
			foreach (Stmt stmt in node.stmts) {
				if (stmt is ClassDef || stmt is VarDef || stmt is FuncDef) {
					continue;
				}
				log.ErrorStmtNotAllowed(stmt.location, NameForStmt(stmt), "inside an extern block");
			}
		} else {
			foreach (Stmt stmt in node.stmts) {
				if (stmt is ExternalStmt || stmt is ClassDef || stmt is VarDef || stmt is FuncDef) {
					continue;
				}
				log.ErrorStmtNotAllowed(stmt.location, NameForStmt(stmt), "at module scope");
			}
		}
		base.Visit(node);
		return null;
	}
	
	public override Null Visit(ExternalStmt node)
	{
		// Nested external statements are disallowed in Visit(Block)
		Push().inExternal = true;
		base.Visit(node);
		stack.Pop();
		return null;
	}
	
	public override Null Visit(FuncDef node)
	{
		// Forbid default arguments for the moment
		foreach (VarDef arg in node.argDefs) {
			if (arg.value != null) {
				log.ErrorDefaultArgNotAllowed(arg.location);
			}
		}
		
		// Validate the presence of the function body
		if (stack.Peek().inExternal != (node.block == null)) {
			log.ErrorFunctionBody(node.location, stack.Peek().inExternal);
		}
		
		Push().Reset().inFunction = true;
		base.Visit(node);
		stack.Pop();
		return null;
	}
	
	public override Null Visit(ClassDef node)
	{
		Push().Reset().inClass = true;
		base.Visit(node);
		stack.Pop();
		return null;
	}
}

////////////////////////////////////////////////////////////////////////////////
// class DefineSymbolsPass
////////////////////////////////////////////////////////////////////////////////

public class DefineSymbolsPass : DefaultVisitor
{
	public Log log;
	
	public DefineSymbolsPass(Log log)
	{
		this.log = log;
	}

	public override Null Visit(Block node)
	{
		// Only make a new scope if our parent node didn't make one already
		if (node.scope == null) {
			node.scope = new Scope(scope, log);
		}
		
		base.Visit(node);
		return null;
	}
	
	public override Null Visit(VarDef node)
	{
		// Define the variable
		node.symbol = new Symbol {
			kind = SymbolKind.Variable,
			def = node,
			type = new ErrorType()
		};
		scope.Define(node.symbol);
		
		base.Visit(node);
		return null;
	}
	
	public override Null Visit(ClassDef node)
	{
		// Define the class
		node.symbol = new Symbol {
			kind = SymbolKind.Class,
			def = node,
			type = new MetaType { instanceType = new ClassType { def = node } }
		};
		scope.Define(node.symbol);
		
		base.Visit(node);
		return null;
	}
	
	public override Null Visit(FuncDef node)
	{
		// Define the function
		node.symbol = new Symbol {
			kind = SymbolKind.Func,
			def = node,
			type = new ErrorType()
		};
		scope.Define(node.symbol);
		
		// Visit the children differently than base.Visit(node) because we
		// want to define arguments in the body scope, not the parent scope
		node.returnType.Accept(this);
		if (node.block != null) {
			// Define arguments in the scope of the body
			node.block.scope = new Scope(scope, log);
			scope = node.block.scope;
			foreach (VarDef argDef in node.argDefs)
				argDef.Accept(this);
			scope = scope.parent;
			node.block.Accept(this);
		} else {
			// Define arguments in a temporary scope if no body is present
			scope = new Scope(scope, log);
			foreach (VarDef argDef in node.argDefs)
				argDef.Accept(this);
			scope = scope.parent;
		}
		
		return null;
	}
}

////////////////////////////////////////////////////////////////////////////////
// class ComputeTypesPass
////////////////////////////////////////////////////////////////////////////////

public class ComputeTypesPass : DefaultVisitor
{
	protected Log log;
	
	public ComputeTypesPass(Log log)
	{
		this.log = log;
	}
	
	public override Null Visit(TypeExpr node)
	{
		node.computedType = node.type;
		return null;
	}
	
	public override Null Visit(BoolExpr node)
	{
		node.computedType = new PrimType { kind = PrimKind.Bool };
		return null;
	}
	
	public override Null Visit(IntExpr node)
	{
		node.computedType = new PrimType { kind = PrimKind.Int };
		return null;
	}

	public override Null Visit(FloatExpr node)
	{
		node.computedType = new PrimType { kind = PrimKind.Float };
		return null;
	}

	public override Null Visit(CharExpr node)
	{
		node.computedType = new PrimType { kind = PrimKind.Char };
		return null;
	}
	
	public override Null Visit(StringExpr node)
	{
		node.computedType = new PrimType { kind = PrimKind.String };
		return null;
	}
	
	public override Null Visit(NullExpr node)
	{
		node.computedType = new ErrorType();
		log.ErrorNoTypeContext(node.location, "null");
		return null;
	}

	public override Null Visit(IdentExpr node)
	{
		node.computedType = new ErrorType();
		Symbol symbol = scope.Lookup(node.name);
		if (symbol != null) {
			node.computedType = symbol.type;
		} else {
			log.ErrorUndefinedSymbol(node.location, node.name);
		}
		return null;
	}
	
	public override Null Visit(BinaryExpr node)
	{
		node.computedType = new ErrorType();
		base.Visit(node);
		return null;
	}

	public override Null Visit(CallExpr node)
	{
		node.computedType = new ErrorType();
		base.Visit(node);
		return null;
	}
	
	public override Null Visit(CastExpr node)
	{
		node.computedType = new ErrorType();
		base.Visit(node);
		return null;
	}
	
	public override Null Visit(MemberExpr node)
	{
		node.computedType = new ErrorType();
		base.Visit(node);
		return null;
	}
	
	public override Null Visit(VarDef node)
	{
		base.Visit(node);
		if (!(node.type.computedType is MetaType)) {
			log.ErrorNotType(node.type.location, node.type.computedType);
		} else {
			node.symbol.type = ((MetaType)node.type.computedType).instanceType;
			if (node.value != null && !node.symbol.type.EqualsType(node.value.computedType)) {
				log.ErrorTypeMismatch(node.location, node.symbol.type, node.value.computedType);
			}
		}
		return null;
	}
}

////////////////////////////////////////////////////////////////////////////////
// Compiler
////////////////////////////////////////////////////////////////////////////////

public static class Compiler
{
	public abstract class Pass
	{
		public abstract bool Apply(Log log, Module module);
	}
	
	public class VisitorPass<T> : Pass
	{
		public Visitor<T> visitor;
		
		public VisitorPass(Visitor<T> visitor)
		{
			this.visitor = visitor;
		}
		
		public override bool Apply(Log log, Module module)
		{
			module.Accept(visitor);
			return log.errors.Count == 0;
		}
	}
	
	public static bool Compile(Log log, Module module)
	{
		Pass[] passes = new Pass[] {
			new VisitorPass<Null>(new StructuralCheckPass(log)),
			new VisitorPass<Null>(new DefineSymbolsPass(log)),
			new VisitorPass<Null>(new ComputeTypesPass(log)),
		};
		foreach (Pass pass in passes) {
			if (!pass.Apply(log, module)) {
				return false;
			}
		}
		return true;
	}
}
