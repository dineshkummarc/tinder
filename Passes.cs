using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

////////////////////////////////////////////////////////////////////////////////
// ErrorMessages
////////////////////////////////////////////////////////////////////////////////

public static class ErrorMessages
{
	private static string WrapType(Type type)
	{
		if (type is MetaType) {
			return "type \"" + type.InstanceType() + "\"";
		}
		return "value of type \"" + type + "\"";
	}
	
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
	
	public static void ErrorNotUseableType(this Log log, Location location, Type type)
	{
		if (type is ErrorType) {
			return;
		}
		log.Error(location, WrapType(type) + " is not a " + (type is MetaType ? "useable type" : "type"));
	}
	
	public static void ErrorBadNullableType(this Log log, Location location, Type type)
	{
		if (type is ErrorType) {
			return;
		}
		if (type is MetaType && type.InstanceType() is NullableType) {
			log.Error(location, WrapType(type) + " is already nullable");
		} else {
			log.Error(location, WrapType(type) + " cannot be nullable");
		}
	}
	
	public static void ErrorTypeMismatch(this Log log, Location location, Type expected, Type found)
	{
		if (expected is ErrorType || found is ErrorType) {
			return;
		}
		log.Error(location, "cannot implicitly convert " + WrapType(found) + " to " + WrapType(expected));
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
		if (node.op == BinaryOp.Assign) {
			log.Error(node.location, "cannot assign " + WrapType(node.right.computedType) + " to " + WrapType(node.left.computedType));
		} else {
			log.Error(node.location, "no match for operator " + node.op.AsString() + " that takes arguments \"(" +
				node.left.computedType + ", " + node.right.computedType + ")\"");
		}
	}
	
	public static void ErrorInvalidCast(this Log log, Location location, Type from, Type to)
	{
		if (from is ErrorType || to is ErrorType) {
			return;
		}
		log.Error(location, "cannot cast value of type \"" + from + "\" to \"" + to + "\"");
	}
	
	public static void ErrorBadSaveDereference(this Log log, Location location, Type type)
	{
		if (type is ErrorType) {
			return;
		}
		log.Error(location, "cannot apply safe dereference operator \"?.\" to " + WrapType(type));
	}
	
	public static void ErrorBadMemberAccess(this Log log, MemberExpr node)
	{
		if (node.obj.computedType is ErrorType) {
			return;
		}
		log.Error(node.location, "cannot access member \"" + node.name + "\" on " + WrapType(node.obj.computedType));
	}

	public static void ErrorCallNotFound(this Log log, Location location, Type funcType, List<Type> argTypes)
	{
		if (funcType is ErrorType || argTypes.Exists(x => x is ErrorType)) {
			return;
		}
		log.Error(location, "cannot call " + WrapType(funcType) + " with arguments \"(" + argTypes.Join() + ")\"");
	}
	
	public static void ErrorMultipleOverloadsFound(this Log log, Location location, List<Type> argTypes)
	{
		if (argTypes.Exists(x => x is ErrorType)) {
			return;
		}
		log.Error(location, "multiple ambiguous overloads that match arguments \"(" + argTypes.Join() + ")\"");
	}
	
	public static void ErrorBadThis(this Log log, Location location)
	{
		log.Error(location, "\"this\" used outside a member function");
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
	
	public static void ErrorNoListContext(this Log log, Location location)
	{
		log.Error(location, "cannot resolve type of list literal without context");
	}
	
	public static void ErrorMetaTypeExpr(this Log log, Location location)
	{
		log.Error(location, "free expression evaluates to type description");
	}
	
	public static void ErrorBadTypeParamCount(this Log log, Location location, int expected, int found, Type type)
	{
		if (type is ErrorType) {
			return;
		}
		if (expected == 0) {
			log.Error(location, "the type \"" + type + "\" does not have free type parameters");
		} else if (found == 0) {
			log.Error(location, "the type \"" + type + "\" requires type parameters");
		} else {
			log.Error(location, "expected " + expected + " type parameters but got " + found);
		}
	}
	
	public static void ErrorBadKeyword(this Log log, Location location, string keyword)
	{
		log.Error(location, "\"" + keyword + "\" is not allowed here");
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
	private Log log;
	
	public StructuralCheckPass(Log log)
	{
		this.log = log;
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
	
	public override Null Visit(Block node)
	{
		// Provide errors for forbidden statements
		if (node.info.funcDef != null) {
			foreach (Stmt stmt in node.stmts) {
				if (stmt is VarDef || stmt is ExprStmt || stmt is IfStmt || stmt is ReturnStmt || stmt is WhileStmt) {
					continue;
				}
				log.ErrorStmtNotAllowed(stmt.location, NameForStmt(stmt), "inside a function body");
			}
		} else if (node.info.classDef != null) {
			foreach (Stmt stmt in node.stmts) {
				if (stmt is ClassDef || stmt is VarDef || stmt is FuncDef) {
					continue;
				}
				log.ErrorStmtNotAllowed(stmt.location, NameForStmt(stmt), "inside a class definition");
			}
		} else if (node.info.inExternal) {
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
	
	public override Null Visit(VarDef node)
	{
		if (node.value != null) {
			if (node.info.inExternal) {
				log.ErrorStmtNotAllowed(node.location, "initialized variable", "inside an external block");
			} else if (node.info.classDef == null && node.info.funcDef == null) {
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
		if (node.info.inExternal != (node.block == null)) {
			log.ErrorFunctionBody(node.location, node.info.inExternal);
		}
		
		base.Visit(node);
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
	private int functionCount;
	
	public ComputeSymbolTypesPass(Log log)
	{
		this.log = log;
		helper = new ComputeTypesPass(log);
	}
	
	public Type GetInstanceType(Expr node, bool isReturnType)
	{
		// Special-case void types for return types
		if (isReturnType && node is TypeExpr && ((TypeExpr)node).type is VoidType) {
			return ((TypeExpr)node).type;
		}
		helper.scope = scope;
		node.Accept(helper);
		if (node.computedType.IsCompleteType()) {
			return node.computedType.InstanceType();
		}
		log.ErrorNotUseableType(node.location, node.computedType);
		return new ErrorType();
	}
	
	public override Null Visit(VarDef node)
	{
		base.Visit(node);
		if (functionCount > 0) {
			node.symbol.type = new ErrorType();
		} else {
			node.symbol.type = GetInstanceType(node.type, false);
		}
		return null;
	}
	
	public override Null Visit(FuncDef node)
	{
		functionCount++;
		base.Visit(node);
		functionCount--;
		node.symbol.type = new FuncType {
			returnType = GetInstanceType(node.returnType, true),
			argTypes = node.argDefs.ConvertAll(arg => GetInstanceType(arg.type, false))
		};
		return null;
	}
}

////////////////////////////////////////////////////////////////////////////////
// ComputeTypesPass
////////////////////////////////////////////////////////////////////////////////

public class ComputeTypesPass : DefaultVisitor
{
	public class Context
	{
		public List<Type> argTypes;
		public Type targetType;
	}
	
	private Log log;
	public Context context;
	
	public ComputeTypesPass(Log log)
	{
		this.log = log;
	}
	
	public override Null Visit(TypeExpr node)
	{
		context = null;
		node.computedType = new ErrorType();
		if (node.type is VoidType) {
			log.ErrorBadKeyword(node.location, "void");
		} else {
			node.computedType = new MetaType { instanceType = node.type };
		}
		return null;
	}
	
	public override Null Visit(VarExpr node)
	{
		context = null;
		log.ErrorBadKeyword(node.location, "var");
		node.computedType = new ErrorType();
		return null;
	}
	
	public override Null Visit(NullExpr node)
	{
		context = null;
		node.computedType = new NullType();
		return null;
	}

	public override Null Visit(ThisExpr node)
	{
		context = null;
		node.computedType = new ErrorType();
		if (node.info.classDef != null && node.info.funcDef != null && !node.info.inStaticFunc) {
			node.computedType = new ClassType { def = node.info.classDef };
		} else {
			log.ErrorBadThis(node.location);
		}
		return null;
	}
	
	public override Null Visit(BoolExpr node)
	{
		context = null;
		node.computedType = new PrimType { kind = PrimKind.Bool };
		return null;
	}
	
	public override Null Visit(IntExpr node)
	{
		context = null;
		node.computedType = new PrimType { kind = PrimKind.Int };
		return null;
	}

	public override Null Visit(FloatExpr node)
	{
		context = null;
		node.computedType = new PrimType { kind = PrimKind.Float };
		return null;
	}

	public override Null Visit(StringExpr node)
	{
		context = null;
		node.computedType = new PrimType { kind = PrimKind.String };
		return null;
	}
	
	public override Null Visit(IdentExpr node)
	{
		List<Type> argTypes = (context == null ? null : context.argTypes);
		
		// Perform the symbol lookup
		context = null;
		node.computedType = new ErrorType();
		node.symbol = scope.Lookup(node.name, LookupKind.Normal);
		if (node.symbol != null) {
			node.computedType = node.symbol.type;
		} else {
			log.ErrorUndefinedSymbol(node.location, node.name);
		}
		
		// Perform overload resolution using information we were provided
		ResolveOverloads(node, argTypes);
		
		return null;
	}
	
	public override Null Visit(ListExpr node)
	{
		node.computedType = new ErrorType();
		
		// Make sure we know what type the list items are supposed to be
		Type targetType = (context == null ? null : context.targetType);
		if (targetType == null) {
			log.ErrorNoListContext(node.location);
			return null;
		}
		if (!(targetType is ListType)) {
			log.ErrorTypeMismatch(node.location, targetType, new ListType());
			return null;
		}
		Type itemType = targetType.ItemType();
		
		// Make sure all items can be converted to that type
		Context itemTypeContext = new Context { targetType = itemType };
		for (int i = 0; i < node.items.Count; i++) {
			Expr item = node.items[i];
			context = itemTypeContext;
			item.Accept(this);
			if (!item.computedType.EqualsType(itemType)) {
				if (item.computedType.CanImplicitlyConvertTo(itemType)) {
					node.items[i] = InsertCast(item, itemType);
				} else {
					log.ErrorTypeMismatch(node.location, itemType, item.computedType);
				}
			}
		}
		
		node.computedType = new ListType { itemType = itemType };
		return null;
	}
	
	public override Null Visit(UnaryExpr node)
	{
		context = null;
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
		context = null;
		node.computedType = new ErrorType();
		base.Visit(node);
		if (!SetUpBinaryOp(node)) {
			log.ErrorBinaryOpNotFound(node);
		}
		return null;
	}

	public override Null Visit(CallExpr node)
	{
		context = null;
		node.computedType = new ErrorType();
		
		// Context is tricky. For overloaded functions, we want to handle
		// arguments first and use them to provide context for overload
		// resolution. For normal functions, we want to handle the function
		// first and use its type to provide context for the arguments.
		// The problem is that we can't know which one to visit without visiting
		// one of them first. To handle this, we temporarily disable logging and
		// check if the function type is OverloadedFuncType or not.
		log.disabled = true;
		node.func.Accept(this);
		log.disabled = false;
		bool isOverload = node.func.computedType is OverloadedFuncType;
		
		if (isOverload) {
			// Visit the arguments first to get context for resolving overloads
			VisitAll(node.args);
			
			// Visit the function last and provide the overload resolving context
			context = new Context { argTypes = node.args.ConvertAll(arg => arg.computedType) };
			node.func.Accept(this);
		} else {
			// Visit the function again, this time with logging
			node.func.Accept(this);
			
			// Visit the arguments and provide context with the argument type
			FuncType funcType = node.func.computedType is FuncType ? (FuncType)node.func.computedType : null;
			for (int i = 0; i < node.args.Count; i++) {
				if (funcType != null && i < funcType.argTypes.Count) {
					context = new Context { targetType = funcType.argTypes[i] };
				}
				node.args[i].Accept(this);
			}
		}
		
		// Cache information for checking
		List<Type> argTypes = node.args.ConvertAll(arg => arg.computedType);
		Type type = node.func.computedType;
		
		// Check for constructors
		if (type.IsCompleteType() && argTypes.Count == 0 && type.InstanceType() is ClassType) {
			node.computedType = type.InstanceType();
			node.isCtor = true;
			return null;
		}
		
		// Call the function if there is one, inserting implicit casts as appropriate
		if (type is FuncType && argTypes.MatchesWithImplicitConversions(type.ArgTypes())) {
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
	
	public override Null Visit(ParamExpr node)
	{
		context = null;
		node.computedType = new ErrorType();
		base.Visit(node);
		
		// Check type parameters first
		foreach (Expr expr in node.typeParams) {
			if (!expr.computedType.IsCompleteType()) {
				log.ErrorNotUseableType(expr.location, expr.computedType);
				return null;
			}
		}
		
		// Check type next, using type parameters to validate
		if (!(node.type.computedType is MetaType)) {
			log.ErrorNotUseableType(node.type.location, node.type.computedType);
			return null;
		}
		Type type = node.type.computedType.InstanceType();
		int paramCountFound = node.typeParams.Count;
		node.computedType = new ErrorType();
		if (type is ListType) {
			int paramCountExpected = (type.ItemType() == null) ? 1 : 0;
			if (paramCountFound != paramCountExpected) {
				log.ErrorBadTypeParamCount(node.location, paramCountExpected, paramCountFound, type);
			} else {
				node.computedType = new MetaType {
					instanceType = new ListType { itemType = node.typeParams[0].computedType.InstanceType() }
				};
			}
		} else if (type is FuncType) {
			if (type.ReturnType() != null) {
				log.ErrorBadTypeParamCount(node.location, 0, paramCountFound, type);
			} else {
				node.computedType = new MetaType {
					instanceType = new FuncType {
						returnType = node.typeParams[0].computedType.InstanceType(),
						argTypes = node.typeParams.GetRange(1, node.typeParams.Count - 1).ConvertAll(x => x.computedType.InstanceType())
					}
				};
			}
		} else {
			log.ErrorBadTypeParamCount(node.location, 0, paramCountFound, type);
		}
		
		return null;
	}
	
	public override Null Visit(CastExpr node)
	{
		context = null;
		node.computedType = new ErrorType();
		node.target.Accept(this);
		
		// Check that the cast is valid
		if (!node.target.computedType.IsCompleteType()) {
			log.ErrorNotUseableType(node.location, node.target.computedType);
		} else {
			Type targetType = node.target.computedType.InstanceType();
			context = new Context { targetType = targetType };
			node.value.Accept(this);
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
		List<Type> argTypes = (context == null ? null : context.argTypes);
		
		context = null;
		node.computedType = new ErrorType();
		base.Visit(node);
		
		// Decide whether to do a static or instance symbol lookup
		LookupKind kind = LookupKind.InstanceMember;
		Type type = node.obj.computedType;
		if (type is MetaType) {
			type = type.InstanceType();
			kind = LookupKind.StaticMember;
		}
		
		// Check for safe dereference
		if (node.isSafeDereference) {
			if (type is NullableType) {
				type = ((NullableType)type).type;
			} else {
				log.ErrorBadSaveDereference(node.location, type);
				return null;
			}
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
		ResolveOverloads(node, argTypes);
		
		// For nullable safe dereference, the result could be null
		if (node.isSafeDereference && !(node.computedType is ErrorType)) {
			node.computedType = node.computedType.AsNullableType();
		}
		
		return null;
	}
	
	public override Null Visit(IndexExpr node)
	{
		context = null;
		node.computedType = new ErrorType();
		base.Visit(node);
		
		if (!(node.obj.computedType is ListType)) {
			log.ErrorTypeMismatch(node.location, new ListType(), node.obj.computedType);
			return null;
		}
		if (!node.index.computedType.IsInt()) {
			log.ErrorTypeMismatch(node.location, new PrimType { kind = PrimKind.Int }, node.index.computedType);
			return null;
		}
		node.computedType = node.obj.computedType.ItemType();
		
		return null;
	}
	
	public override Null Visit(NullableExpr node)
	{
		context = null;
		node.computedType = new ErrorType();
		base.Visit(node);
		
		if (node.value.computedType is MetaType) {
			Type type = node.value.computedType.InstanceType();
			if (type is NullableType) {
				log.ErrorBadNullableType(node.value.location, node.value.computedType);
			} else {
				node.computedType = new MetaType { instanceType = type.AsNullableType() };
			}
		} else {
			log.ErrorNotUseableType(node.value.location, node.value.computedType);
		}
		
		return null;
	}
	
	public override Null Visit(ReturnStmt node)
	{
		Type returnType = node.info.funcDef.symbol.type.ReturnType();
		context = new Context { targetType = returnType };
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
	
	public override Null Visit(ExprStmt node)
	{
		base.Visit(node);
		if (node.value.computedType is MetaType) {
			log.ErrorMetaTypeExpr(node.location);
		}
		return null;
	}
	
	public override Null Visit(VarDef node)
	{
		// Handle type inference separately
		if (node.type is VarExpr && node.value != null) {
			node.value.Accept(this);
			if (node.value.computedType is NullType || node.value.computedType is VoidType) {
				log.ErrorNotUseableType(node.location, new MetaType { instanceType = node.value.computedType });
				node.symbol.type = new ErrorType();
			} else {
				node.symbol.type = node.value.computedType;
			}
			return null;
		}
		
		node.type.Accept(this);
		if (!node.type.computedType.IsCompleteType()) {
			log.ErrorNotUseableType(node.type.location, node.type.computedType);
		} else {
			node.symbol.type = node.type.computedType.InstanceType();
			if (node.value != null) {
				// Provide the variable type as the context to resolve the value type
				context = new Context { targetType = node.symbol.type };
				node.value.Accept(this);
				if (!node.value.computedType.EqualsType(node.symbol.type)) {
					if (node.value.computedType.CanImplicitlyConvertTo(node.symbol.type)) {
						node.value = InsertCast(node.value, node.symbol.type);
					} else {
						log.ErrorTypeMismatch(node.location, node.symbol.type, node.value.computedType);
					}
				}
			}
		}
		return null;
	}
	
	public override Null Visit(FuncDef node)
	{
		// Special-case void types for return types
		if (node.returnType is TypeExpr && ((TypeExpr)node.returnType).type is VoidType) {
			node.returnType.computedType = new MetaType { instanceType = ((TypeExpr)node.returnType).type };
		} else {
			node.returnType.Accept(this);
		}
		VisitAll(node.argDefs);
		if (node.block != null) {
			node.block.Accept(this);
		}
		return null;
	}
	
	public override Null Visit(IfStmt node)
	{
		base.Visit(node);
		if (!node.test.computedType.IsBool()) {
			log.ErrorTypeMismatch(node.test.location, new PrimType { kind = PrimKind.Bool }, node.test.computedType);
		}
		return null;
	}
	
	public override Null Visit(WhileStmt node)
	{
		base.Visit(node);
		if (!node.test.computedType.IsBool()) {
			log.ErrorTypeMismatch(node.test.location, new PrimType { kind = PrimKind.Bool }, node.test.computedType);
		}
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
		
		// Binary operators aren't supported on type literals
		if (left is MetaType || right is MetaType) {
			return false;
		}
		
		switch (node.op) {
			case BinaryOp.Assign:
				if (left.EqualsType(right)) {
					node.computedType = left;
					return true;
				} else if (right.CanImplicitlyConvertTo(left)) {
					node.right = InsertCast(node.right, left);
					node.computedType = left;
					return true;
				}
				break;
				
			case BinaryOp.NullableDefault:
				if (left is NullableType) {
					Type type = ((NullableType)left).type;
					if (right.EqualsType(type)) {
						node.computedType = type;
						return true;
					} else if (right.CanImplicitlyConvertTo(type)) {
						node.right = InsertCast(node.right, type);
						node.computedType = type;
						return true;
					}
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
		
			case BinaryOp.LShift:
			case BinaryOp.RShift:
			case BinaryOp.BitAnd:
			case BinaryOp.BitOr:
			case BinaryOp.BitXor:
				if (left.IsInt() && right.IsInt() && SetUpBinaryOpHelper(node, false)) {
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
			Type type = node.type.computedType.InstanceType();
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
// RenameSymbolsPass
////////////////////////////////////////////////////////////////////////////////

public class RenameSymbolsPass : DefaultVisitor
{
	private static readonly Regex splitter = new Regex("[<>,. ]+");
	private HashSet<string> reservedWords;
	private bool renameOverloads;
	
	public RenameSymbolsPass(HashSet<string> reservedWords, bool renameOverloads)
	{
		this.reservedWords = reservedWords;
		this.renameOverloads = renameOverloads;
	}
	
	private string MangleOverload(string name, List<Type> argTypes)
	{
		foreach (Type type in argTypes) {
			List<string> parts = new List<string>(splitter.Split(type.ToString()));
			name += parts.ConvertAll(x => x.Length == 0 ? "" : x.Substring(0, 1).ToUpper() + x.Substring(1)).Join();
		}
		return name;
	}
	
	private string Rename(string name, Scope scope)
	{
		while (reservedWords.Contains(name) || scope.Lookup(name, LookupKind.Any) != null) {
			name = "_" + name;
		}
		return name;
	}
	
	public override Null Visit(Block node)
	{
		foreach (KeyValuePair<string, Symbol> pair in node.scope.map.Items()) {
			// See if we have to rename
			string oldName = pair.Key;
			Symbol symbol = pair.Value;
			if (!reservedWords.Contains(oldName) && (!renameOverloads || symbol.kind != SymbolKind.OverloadedFunc)) {
				continue;
			}
			
			// If we need to rename, remove the symbol first so lookups will return useful info
			node.scope.map.Remove(oldName);
			symbol.finalName = Rename(oldName, node.scope);
			if (!renameOverloads || symbol.kind != SymbolKind.OverloadedFunc) {
				node.scope.map.Add(symbol.finalName, symbol);
			}
			
			// Rename all overloads
			if (symbol.kind == SymbolKind.OverloadedFunc) {
				foreach (Symbol overload in ((OverloadedFuncType)symbol.type).overloads) {
					if (renameOverloads) {
						overload.finalName = Rename(MangleOverload(overload.def.name, overload.type.ArgTypes()), node.scope);
						node.scope.map.Add(overload.finalName, overload);
					} else {
						overload.finalName = symbol.finalName;
					}
				}
			}
		}
		base.Visit(node);
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
