using System;
using System.Collections.Generic;

////////////////////////////////////////////////////////////////////////////////
// ErrorMessages
////////////////////////////////////////////////////////////////////////////////

public static class ErrorMessages
{
	public static void ErrorRedefinition(this Log log, Location location, string name)
	{
		log.Error(location, "redefinition of " + name + " in the same scope");
	}
	
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
	
	public static void ErrorNotType(this Log log, Location location, Type type)
	{
		if (type is ErrorType) {
			return;
		}
		log.Error(location, "value of type \"" + type + "\" is not a type");
	}
	
	public static void ErrorTypeMismatch(this Log log, Location location, Type expected, Type found)
	{
		if (expected is ErrorType || found is ErrorType) {
			return;
		}
		log.Error(location, "expected value of type \"" + expected + "\" but found value of type \"" + found + "\"");
	}
	
	public static void ErrorUnaryOpNotFound(this Log log, UnaryExpr node)
	{
		if (node.value.computedType is ErrorType) {
			return;
		}
		log.Error(node.location, "no match for operator " + node.op.AsString() + " that takes arguments \"(" +
			node.value.computedType + ")\"");
	}
	
	public static void ErrorBinaryOpNotFound(this Log log, BinaryExpr node)
	{
		if (node.left.computedType is ErrorType || node.right.computedType is ErrorType) {
			return;
		}
		log.Error(node.location, "no match for operator " + node.op.AsString() + " that takes arguments \"(" +
			node.left.computedType + ", " + node.right.computedType + ")\"");
	}
	
	public static void ErrorInvalidCast(this Log log, Location location, Type from, Type to)
	{
		if (from is ErrorType || to is ErrorType) {
			return;
		}
		log.Error(location, "cannot cast value of type \"" + from + "\" to \"" + to + "\"");
	}
	
	public static void ErrorBadMemberAccess(this Log log, MemberExpr node)
	{
		if (node.obj.computedType is ErrorType) {
			return;
		}
		log.Error(node.location, "cannot access member \"" + node.name + "\" on value of type \"" +
			node.obj.computedType + "\"");
	}

	public static void ErrorCallNotFound(this Log log, Location location, Type funcType, List<Type> argTypes)
	{
		if (funcType is ErrorType || argTypes.Exists(x => x is ErrorType)) {
			return;
		}
		log.Error(location, "cannot call value of type \"" + funcType + "\" with arguments \"" + argTypes.AsString() + "\"");
	}
	
	public static void ErrorMultipleOverloadsFound(this Log log, Location location, List<Type> argTypes)
	{
		if (argTypes.Exists(x => x is ErrorType)) {
			return;
		}
		log.Error(location, "multiple ambiguous overloads that match arguments \"" + argTypes.AsString() + "\"");
	}
	
	public static void ErrorThisOutsideClass(this Log log, Location location)
	{
		log.Error(location, "\"this\" used outside class definition");
	}
	
	public static void ErrorVoidReturn(this Log log, Location location, bool shouldBeVoid)
	{
		if (shouldBeVoid) {
			log.Error(location, "returning value from function returning void");
		} else {
			log.Error(location, "missing return value in non-void function");
		}
	}
	
	public static void ErrorNotAllPathsReturnValue(this Log log, Location location)
	{
		log.Error(location, "not all control paths return a value");
	}
	
	public static void ErrorUseBeforeDefinition(this Log log, Location location, string name)
	{
		log.Error(location, "use of variable \"" + name + "\" before its definition");
	}
	
	public static void ErrorOverloadChangedModifier(this Log log, Location location, string modifier)
	{
		log.Error(location, "overload has different " + modifier + " modifier than previous overload");
	}
	
	public static void ErrorNoOverloadContext(this Log log, Location location)
	{
		log.Error(location, "cannot resolve overloaded function without context");
	}
	
	public static void WarningDeadCode(this Log log, Location location)
	{
		log.Warning(location, "dead code");
	}
}

////////////////////////////////////////////////////////////////////////////////
// StructuralCheckPass
////////////////////////////////////////////////////////////////////////////////

public class StructuralCheckPass : DefaultVisitor
{
	private class State
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
	
	private Log log;
	private Stack<State> stack;
	
	public StructuralCheckPass(Log log)
	{
		this.log = log;
		stack = new Stack<State>();
		stack.Push(new State());
	}
	
	private string NameForStmt(Stmt stmt)
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
		if (stmt is WhileStmt) {
			return "while block";
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
	
	private State Push()
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
				if (stmt is VarDef || stmt is ExprStmt || stmt is IfStmt || stmt is ReturnStmt || stmt is WhileStmt) {
					continue;
				}
				log.ErrorStmtNotAllowed(stmt.location, NameForStmt(stmt), "inside a function body");
			}
		} else if (stack.Peek().inExternal) {
			foreach (Stmt stmt in node.stmts) {
				if (stmt is ClassDef || stmt is VarDef || stmt is FuncDef) {
					continue;
				}
				log.ErrorStmtNotAllowed(stmt.location, NameForStmt(stmt), "inside an external block");
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
	
	public override Null Visit(VarDef node)
	{
		if (node.value != null) {
			if (stack.Peek().inExternal) {
				log.ErrorStmtNotAllowed(node.location, "initialized variable", "inside an external block");
			} else if (!stack.Peek().inClass && !stack.Peek().inFunction) {
				log.ErrorStmtNotAllowed(node.location, "initialized variable", "at module scope");
			}
		}
		
		base.Visit(node);
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
// DefineSymbolsPass
////////////////////////////////////////////////////////////////////////////////

public class DefineSymbolsPass : DefaultVisitor
{
	private Log log;
	
	public DefineSymbolsPass(Log log)
	{
		this.log = log;
	}

	public override Null Visit(Block node)
	{
		// Only make a new scope if our parent node didn't make one already
		if (node.scope == null) {
			node.scope = new Scope(scope, log, ScopeKind.Local);
		}
		
		base.Visit(node);
		return null;
	}
	
	public override Null Visit(Module node)
	{
		// Make a module scope
		node.block.scope = new Scope(null, log, ScopeKind.Module);
		
		base.Visit(node);
		return null;
	}
	
	public override Null Visit(VarDef node)
	{
		// Define the variable
		node.symbol = new Symbol {
			kind = SymbolKind.Variable,
			isStatic = false,
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
			isStatic = true,
			def = node,
			type = new MetaType { instanceType = new ClassType { def = node } }
		};
		scope.Define(node.symbol);
		
		// Make a class scope
		node.block.scope = new Scope(scope, log, ScopeKind.Class);
		
		base.Visit(node);
		return null;
	}
	
	public override Null Visit(FuncDef node)
	{
		// Define the function
		node.symbol = new Symbol {
			kind = SymbolKind.Func,
			isStatic = node.isStatic,
			def = node,
			type = new ErrorType()
		};
		scope.Define(node.symbol);
		
		// Visit the children differently than base.Visit(node) because we
		// want to define arguments in the body scope, not the parent scope
		node.returnType.Accept(this);
		if (node.block != null) {
			// Define arguments in the scope of the body
			node.block.scope = new Scope(scope, log, ScopeKind.Func);
			scope = node.block.scope;
			foreach (VarDef argDef in node.argDefs)
				argDef.Accept(this);
			scope = scope.parent;
			node.block.Accept(this);
		} else {
			// Define arguments in a temporary scope if no body is present
			scope = new Scope(scope, log, ScopeKind.Func);
			foreach (VarDef argDef in node.argDefs)
				argDef.Accept(this);
			scope = scope.parent;
		}
		
		return null;
	}
	
	public override Null Visit(ExternalStmt node)
	{
		// External statements don't have their own scope
		node.block.scope = scope;
		base.Visit(node);
		return null;
	}
}

////////////////////////////////////////////////////////////////////////////////
// ComputeSymbolTypesPass
////////////////////////////////////////////////////////////////////////////////

// Compute the type of all expressions that are expected to contain types, then
// use these to set symbol types. This way the types of all symbols will be
// known when we compute the types of the other expressions in a later pass.
public class ComputeSymbolTypesPass : DefaultVisitor
{
	private Log log;
	private ComputeTypesPass helper;
	
	public ComputeSymbolTypesPass(Log log)
	{
		this.log = log;
		helper = new ComputeTypesPass(log);
	}
	
	public Type GetInstanceType(Expr node)
	{
		helper.scope = scope;
		node.Accept(helper);
		if (node.computedType is MetaType) {
			return ((MetaType)node.computedType).instanceType;
		}
		log.ErrorNotType(node.location, node.computedType);
		return new ErrorType();
	}
	
	public override Null Visit(VarDef node)
	{
		base.Visit(node);
		node.symbol.type = GetInstanceType(node.type);
		return null;
	}
	
	public override Null Visit(FuncDef node)
	{
		base.Visit(node);
		node.symbol.type = new FuncType {
			returnType = GetInstanceType(node.returnType),
			argTypes = node.argDefs.ConvertAll(arg => GetInstanceType(arg.type))
		};
		return null;
	}
}

////////////////////////////////////////////////////////////////////////////////
// ComputeTypesPass
////////////////////////////////////////////////////////////////////////////////

public class ComputeTypesPass : DefaultVisitor
{
	private Log log;
	private Type returnType;
	private ClassType thisType;
	public List<Type> overloadContext;
	
	public ComputeTypesPass(Log log)
	{
		this.log = log;
	}
	
	public override Null Visit(TypeExpr node)
	{
		overloadContext = null;
		node.computedType = node.type;
		return null;
	}
	
	public override Null Visit(NullExpr node)
	{
		overloadContext = null;
		node.computedType = new NullType();
		return null;
	}

	public override Null Visit(ThisExpr node)
	{
		overloadContext = null;
		node.computedType = new ErrorType();
		if (thisType != null) {
			node.computedType = thisType;
		} else {
			log.ErrorThisOutsideClass(node.location);
		}
		return null;
	}

	public override Null Visit(BoolExpr node)
	{
		overloadContext = null;
		node.computedType = new PrimType { kind = PrimKind.Bool };
		return null;
	}
	
	public override Null Visit(IntExpr node)
	{
		overloadContext = null;
		node.computedType = new PrimType { kind = PrimKind.Int };
		return null;
	}

	public override Null Visit(FloatExpr node)
	{
		overloadContext = null;
		node.computedType = new PrimType { kind = PrimKind.Float };
		return null;
	}

	public override Null Visit(StringExpr node)
	{
		overloadContext = null;
		node.computedType = new PrimType { kind = PrimKind.String };
		return null;
	}
	
	public override Null Visit(IdentExpr node)
	{
		List<Type> providedOverloadContext = overloadContext;
		
		// Perform the symbol lookup
		overloadContext = null;
		node.computedType = new ErrorType();
		node.symbol = scope.Lookup(node.name, LookupKind.Normal);
		if (node.symbol != null) {
			node.computedType = node.symbol.type;
		} else {
			log.ErrorUndefinedSymbol(node.location, node.name);
		}
		
		// Perform overload resolution using information we were provided
		ResolveOverloads(node, providedOverloadContext);
		
		return null;
	}
	
	public override Null Visit(UnaryExpr node)
	{
		overloadContext = null;
		node.computedType = new ErrorType();
		base.Visit(node);
		
		switch (node.op) {
			case UnaryOp.Negative:
				if (node.value.computedType.IsNumeric()) {
					node.computedType = node.value.computedType;
				} else {
					log.ErrorUnaryOpNotFound(node);
				}
				break;
			case UnaryOp.Not:
				if (node.value.computedType.IsBool()) {
					node.computedType = node.value.computedType;
				} else {
					log.ErrorUnaryOpNotFound(node);
				}
				break;
		}
		
		return null;
	}
	
	public override Null Visit(BinaryExpr node)
	{
		overloadContext = null;
		node.computedType = new ErrorType();
		base.Visit(node);
		if (!SetUpBinaryOp(node)) {
			log.ErrorBinaryOpNotFound(node);
		}
		return null;
	}

	public override Null Visit(CallExpr node)
	{
		overloadContext = null;
		node.computedType = new ErrorType();
		
		// Visit the arguments first to get context for resolving overloads
		VisitAll(node.args);
		List<Type> argTypes = node.args.ConvertAll(arg => arg.computedType);
		
		// Visit the function last and provide the overload resolving context
		overloadContext = argTypes;
		node.func.Accept(this);
		Type type = node.func.computedType;
		
		// Check for constructors
		if (type is MetaType && argTypes.Count == 0 && ((MetaType)type).instanceType is ClassType) {
			node.computedType = ((MetaType)type).instanceType;
			return null;
		}
		
		// Call the function if there is one, inserting implicit casts as appropriate
		if (type is FuncType && argTypes.MatchesWithImplicitConversions(((FuncType)type).argTypes)) {
			FuncType funcType = (FuncType)type;
			for (int i = 0; i < funcType.argTypes.Count; i++) {
				if (!node.args[i].computedType.EqualsType(funcType.argTypes[i])) {
					node.args[i] = InsertCast(node.args[i], funcType.argTypes[i]);
				}
			}
			node.computedType = funcType.returnType;
		} else {
			log.ErrorCallNotFound(node.location, type, argTypes);
		}
		
		return null;
	}
	
	public override Null Visit(CastExpr node)
	{
		overloadContext = null;
		node.computedType = new ErrorType();
		base.Visit(node);
		
		// Check that the cast is valid
		if (!(node.target.computedType is MetaType)) {
			log.ErrorNotType(node.location, node.target.computedType);
		} else {
			Type targetType = ((MetaType)node.target.computedType).instanceType;
			if (!IsValidCast(node.value.computedType, targetType)) {
				log.ErrorInvalidCast(node.value.location, node.value.computedType, targetType);
			} else {
				node.computedType = targetType;
			}
		}
		
		return null;
	}
	
	public override Null Visit(MemberExpr node)
	{
		List<Type> providedOverloadContext = overloadContext;
		
		overloadContext = null;
		node.computedType = new ErrorType();
		base.Visit(node);
		
		// Decide whether to do a static or instance symbol lookup
		LookupKind kind = LookupKind.InstanceMember;
		Type type = node.obj.computedType;
		if (type is MetaType) {
			type = ((MetaType)type).instanceType;
			kind = LookupKind.StaticMember;
		}
		
		// Perform the symbol lookup
		if (type is ClassType) {
			node.symbol = ((ClassType)type).def.block.scope.Lookup(node.name, kind);
			if (node.symbol == null) {
				log.ErrorBadMemberAccess(node);
			} else {
				node.computedType = node.symbol.type;
			}
		} else {
			log.ErrorBadMemberAccess(node);
		}
		
		// Perform overload resolution using information we were provided
		ResolveOverloads(node, providedOverloadContext);
		
		return null;
	}
	
	public override Null Visit(ReturnStmt node)
	{
		overloadContext = null;
		base.Visit(node);
		if ((node.value == null) != (returnType is VoidType)) {
			log.ErrorVoidReturn(node.location, returnType is VoidType);
		} else if (node.value != null && !node.value.computedType.EqualsType(returnType)) {
			if (node.value.computedType.CanImplicitlyConvertTo(returnType)) {
				node.value = InsertCast(node.value, returnType);
			} else {
				log.ErrorTypeMismatch(node.location, returnType, node.value.computedType);
			}
		}
		return null;
	}
	
	public override Null Visit(VarDef node)
	{
		overloadContext = null;
		base.Visit(node);
		if (!(node.type.computedType is MetaType)) {
			log.ErrorNotType(node.type.location, node.type.computedType);
		} else {
			node.symbol.type = ((MetaType)node.type.computedType).instanceType;
			if (node.value != null && !node.value.computedType.EqualsType(node.symbol.type)) {
				if (node.value.computedType.CanImplicitlyConvertTo(node.symbol.type)) {
					node.value = InsertCast(node.value, node.symbol.type);
				} else {
					log.ErrorTypeMismatch(node.location, node.symbol.type, node.value.computedType);
				}
			}
		}
		return null;
	}
	
	public override Null Visit(FuncDef node)
	{
		overloadContext = null;
		Type old = returnType;
		returnType = ((FuncType)node.symbol.type).returnType;
		base.Visit(node);
		returnType = old;
		return null;
	}
	
	public override Null Visit(ClassDef node)
	{
		overloadContext = null;
		ClassType old = thisType;
		thisType = new ClassType { def = node };
		base.Visit(node);
		thisType = old;
		return null;
	}
	
	private void ResolveOverloads(Expr node, List<Type> argTypes)
	{
		if (!(node.computedType is OverloadedFuncType)) {
			return;
		}
		
		if (argTypes == null) {
			log.ErrorNoOverloadContext(node.location);
			return;
		}
		
		// Try to resolve overloaded functions
		OverloadedFuncType overloadedType = (OverloadedFuncType)node.computedType;
		List<Symbol> exactMatches = new List<Symbol>();
		List<Symbol> implicitMatches = new List<Symbol>();
		
		// Try to match each overload
		foreach (Symbol symbol in overloadedType.overloads) {
			FuncType funcType = (FuncType)symbol.type;
			if (argTypes.MatchesExactly(funcType.argTypes)) {
				exactMatches.Add(symbol);
			} else if (argTypes.MatchesWithImplicitConversions(funcType.argTypes)) {
				implicitMatches.Add(symbol);
			}
		}
		
		// Pick the best-matching overload
		List<Symbol> matches = (exactMatches.Count > 0) ? exactMatches : implicitMatches;
		if (matches.Count > 1) {
			log.ErrorMultipleOverloadsFound(node.location, argTypes);
		} else if (matches.Count == 1) {
			node.computedType = matches[0].type;
			
			// Store the resolved symbol
			if (node is IdentExpr) {
				((IdentExpr)node).symbol = matches[0];
			} else if (node is MemberExpr) {
				((MemberExpr)node).symbol = matches[0];
			}
		}
	}
	
	private static Expr InsertCast(Expr value, Type target)
	{
		Type type = new MetaType { instanceType = target };
		return new CastExpr {
			location = value.location,
			value = value,
			target = new TypeExpr { type = type, computedType = type },
			computedType = target
		};
	}
	
	private bool IsValidCast(Type from, Type to)
	{
		return from.EqualsType(to) || from.CanImplicitlyConvertTo(to) || (from.IsNumeric() && to.IsNumeric());
	}
	
	private bool SetUpBinaryOpHelper(BinaryExpr node, bool resultIsBool)
	{
		Type left = node.left.computedType;
		Type right = node.right.computedType;
		
		if (left.EqualsType(right)) {
			node.computedType = resultIsBool ? new PrimType { kind = PrimKind.Bool } : left;
			return true;
		}
		
		if (left.CanImplicitlyConvertTo(right)) {
			node.left = InsertCast(node.left, right);
			node.computedType = resultIsBool ? new PrimType { kind = PrimKind.Bool } : right;
			return true;
		}
		
		if (right.CanImplicitlyConvertTo(left)) {
			node.right = InsertCast(node.right, left);
			node.computedType = resultIsBool ? new PrimType { kind = PrimKind.Bool } : left;
			return true;
		}
		
		return false;
	}
	
	private bool SetUpBinaryOp(BinaryExpr node)
	{
		Type left = node.left.computedType;
		Type right = node.right.computedType;
		
		switch (node.op) {
			case BinaryOp.Assign:
				if (left.EqualsType(right)) {
					node.computedType = new VoidType();
					return true;
				} else if (left.CanImplicitlyConvertTo(right)) {
					node.right = InsertCast(node.right, left);
					node.computedType = new VoidType();
					return true;
				}
				break;
		
			case BinaryOp.And:
			case BinaryOp.Or:
				if (left.IsBool() && right.IsBool()) {
					node.computedType = new PrimType { kind = PrimKind.Bool };
					return true;
				}
				break;
		
			case BinaryOp.Add:
				if (((left.IsNumeric() && right.IsNumeric()) || (left.IsString() && right.IsString())) && SetUpBinaryOpHelper(node, false)) {
					return true;
				}
				break;
				
			case BinaryOp.Subtract:
			case BinaryOp.Multiply:
			case BinaryOp.Divide:
				if (left.IsNumeric() && right.IsNumeric() && SetUpBinaryOpHelper(node, false)) {
					return true;
				}
				break;
		
			case BinaryOp.Equal:
			case BinaryOp.NotEqual:
				if (SetUpBinaryOpHelper(node, true)) {
					return true;
				}
				break;
				
			case BinaryOp.LessThan:
			case BinaryOp.GreaterThan:
			case BinaryOp.LessThanEqual:
			case BinaryOp.GreaterThanEqual:
				if (((left.IsNumeric() && right.IsNumeric()) || (left.IsString() && right.IsString())) && SetUpBinaryOpHelper(node, true)) {
					return true;
				}
				break;
		}
		
		return false;
	}
}

////////////////////////////////////////////////////////////////////////////////
// FlowValidationPass
////////////////////////////////////////////////////////////////////////////////

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
		if (!(((FuncType)node.symbol.type).returnType is VoidType) && !stack.Peek().didReturn) {
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

////////////////////////////////////////////////////////////////////////////////
// DefaultInitializePass
////////////////////////////////////////////////////////////////////////////////

public class DefaultInitializePass : DefaultVisitor
{
	public override Null Visit(ExternalStmt node)
	{
		// Initialization isn't possible in external statements
		return null;
	}
	
	public override Null Visit(FuncDef node)
	{
		// Don't initialize arguments
		node.block.Accept(this);
		return null;
	}
	
	public override Null Visit(VarDef node)
	{
		if (node.value == null) {
			Type type = ((MetaType)node.type.computedType).instanceType;
			if (type.IsBool()) {
				node.value = new BoolExpr { value = false, computedType = type, location = node.location };
			} else if (type.IsInt()) {
				node.value = new IntExpr { value = 0, computedType = type, location = node.location };
			} else if (type.IsFloat()) {
				node.value = new FloatExpr { value = 0, computedType = type, location = node.location };
			} else if (type.IsString()) {
				node.value = new StringExpr { value = "", computedType = type, location = node.location };
			} else {
				node.value = new NullExpr { computedType = type, location = node.location };
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
			new VisitorPass<Null>(new ComputeSymbolTypesPass(log)),
			new VisitorPass<Null>(new ComputeTypesPass(log)),
			new VisitorPass<Null>(new FlowValidationPass(log)),
			new VisitorPass<Null>(new DefaultInitializePass()),
		};
		foreach (Pass pass in passes) {
			if (!pass.Apply(log, module)) {
				return false;
			}
		}
		return true;
	}
}
