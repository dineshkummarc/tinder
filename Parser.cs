using System;
using System.Collections.Generic;

// A pratt parser is a parser that associates semantics with tokens instead of
// with grammar rules. Pratt parsers simplify and speed up expression parsing
// when many operators with varying precedence levels are involved.
public class PrattParser
{
	public class Symbol
	{
		// The left binding power controls the precedence level of the symbol
		// when the left denotation is triggered
		public int leftBindingPower;
		
		// Used for operands and unary prefix operators ("nud" in classic
		// Pratt parser terminology)
		public Func<ParserContext, Expr> prefixParser;
		
		// Used for binary infix operators and unary postfix operators
		// ("led" in classic Pratt parser terminology)
		public Func<ParserContext, Expr, Expr> infixParser;
	}
	
	private Dictionary<TokenKind, Symbol> table = new Dictionary<TokenKind, Symbol>();
	
	// Attempt to parse an expression from the provided parser context at the
	// provided binding power (precedence level). Returns an object on success
	// or null on failure.
	public Expr Parse(ParserContext context, int rightBindingPower = 0)
	{
		Symbol symbol;
		if (!table.TryGetValue(context.CurrentToken().kind, out symbol) || symbol.prefixParser == null) {
			return null;
		}
		Expr left = symbol.prefixParser(context);
		while (left != null) {
			if (!table.TryGetValue(context.CurrentToken().kind, out symbol) || symbol.infixParser == null ||
			    	rightBindingPower >= symbol.leftBindingPower) {
				break;
			}
			left = symbol.infixParser(context, left);
		}
		return left;
	}
	
	// Return the symbol that matches tokens of the given kind, creating it if
	// needed. The binding power of the returned symbol will be at least as
	// high as the provided binding power, but may be higher due to a previous
	// Get() call.
	public Symbol Get(TokenKind kind, int bindingPower = 0)
	{
		Symbol symbol;
		if (!table.TryGetValue(kind, out symbol)) {
			table.Add(kind, symbol = new Symbol());
		}
		symbol.leftBindingPower = Math.Max(symbol.leftBindingPower, bindingPower);
		return symbol;
	}
	
	// Create a literal operand that returns the result of applying func to the
	// matched token.
	public Symbol Literal(TokenKind kind, Func<Token, Expr> func)
	{
		Symbol symbol = Get(kind);
		symbol.prefixParser = (ParserContext context) => {
			Token token = context.CurrentToken();
			context.Next();
			return func(token);
		};
		return symbol;
	}
	
	// Create a binary infix operator with a certain binding power (precedence
	// level) that returns the result of applying func to the left expression,
	// the token, and the right expression.
	public Symbol Infix(TokenKind kind, int bindingPower, Func<Expr, Token, Expr, Expr> func)
	{
		Symbol symbol = Get(kind, bindingPower);
		symbol.infixParser = (ParserContext context, Expr left) => {
			Token token = context.CurrentToken();
			context.Next();
			Expr right = Parse(context, bindingPower);
			return right != null ? func(left, token, right) : null;
		};
		return symbol;
	}
	
	// Create a binary infix operator with a certain binding power (precedence
	// level) that returns the result of applying func to the left expression,
	// the token, and the right expression.
	public Symbol Prefix(TokenKind kind, int bindingPower, Func<Token, Expr, Expr> func)
	{
		Symbol symbol = Get(kind);
		symbol.prefixParser = (ParserContext context) => {
			Token token = context.CurrentToken();
			context.Next();
			Expr right = Parse(context, bindingPower);
			return right != null ? func(token, right) : null;
		};
		return symbol;
	}
}

public class ParserContext
{
	private int index;
	private List<Token> tokens;
	
	public ParserContext(List<Token> tokens)
	{
		this.tokens = tokens;
	}
	
	public Token CurrentToken()
	{
		return tokens[index];
	}
	
	public int CurrentIndex()
	{
		return index;
	}
	
	public bool Peek(TokenKind kind)
	{
		if (tokens[index].kind == kind) {
			return true;
		}
		return false;
	}
	
	public bool Consume(TokenKind kind)
	{
		if (Peek(kind)) {
			Next();
			return true;
		}
		return false;
	}
	
	public void Next()
	{
		if (index + 1 < tokens.Count) {
			index++;
		}
	}
}

public static class Parser
{
	private static readonly PrattParser pratt = new PrattParser();
	
	static Parser()
	{
		// Literals
		pratt.Literal(TokenKind.Null, (Token token) => new NullExpr { location = token.location });
		pratt.Literal(TokenKind.This, (Token token) => new ThisExpr { location = token.location });
		pratt.Literal(TokenKind.True, (Token token) => new BoolExpr { location = token.location, value = true });
		pratt.Literal(TokenKind.False, (Token token) => new BoolExpr { location = token.location, value = false });
		pratt.Literal(TokenKind.StringLit, (Token token) => new StringExpr { location = token.location, value = token.text });
		pratt.Literal(TokenKind.Identifier, (Token token) => new IdentExpr { location = token.location, name = token.text });
		pratt.Literal(TokenKind.CharLit, (Token token) => new IntExpr { location = token.location, value = token.text[0] });
		pratt.Get(TokenKind.IntLit).prefixParser = ParseIntExpr;
		pratt.Get(TokenKind.FloatLit).prefixParser = ParseFloatExpr;
		
		// Types
		pratt.Literal(TokenKind.Void, ParseVoidType);
		pratt.Literal(TokenKind.Bool, ParsePrimType);
		pratt.Literal(TokenKind.Int, ParsePrimType);
		pratt.Literal(TokenKind.Float, ParsePrimType);
		pratt.Literal(TokenKind.String, ParsePrimType);
		
		// Infix operators
		foreach (OperatorInfo<BinaryOp> info in Constants.binaryOperators)
			pratt.Infix(info.kind, info.precedence, ParseBinaryExpr);
		
		// Prefix operators
		foreach (OperatorInfo<UnaryOp> info in Constants.unaryOperators)
			pratt.Prefix(info.kind, info.precedence, ParsePrefixExpr);
		
		// Parsers requiring special behavior
		pratt.Get(TokenKind.LParen).prefixParser = ParseGroup;
		pratt.Get(TokenKind.As, Constants.operatorPrecedence[TokenKind.As]).infixParser = ParseCastExpr;
		pratt.Get(TokenKind.Dot, Constants.operatorPrecedence[TokenKind.Dot]).infixParser = ParseMemberExpr;
		pratt.Get(TokenKind.LParen, Constants.operatorPrecedence[TokenKind.LParen]).infixParser = ParseCallExpr;
	}
	
	private static IntExpr ParseIntExpr(ParserContext context)
	{
		int value;
		Token token = context.CurrentToken();
		try {
			if (token.text.StartsWith("0x")) {
				value = Convert.ToInt32(token.text.Substring(2), 16);
			} else if (token.text.StartsWith("0o")) {
				value = Convert.ToInt32(token.text.Substring(2), 8);
			} else if (token.text.StartsWith("0b")) {
				value = Convert.ToInt32(token.text.Substring(2), 2);
			} else {
				value = int.Parse(token.text);
			}
		} catch (Exception) {
			return null;
		}
		context.Next();
		return new IntExpr {
			location = token.location,
			value = value
		};
	}
	
	private static FloatExpr ParseFloatExpr(ParserContext context)
	{
		float value;
		Token token = context.CurrentToken();
		try {
			value = float.Parse(token.text);
		} catch (Exception) {
			return null;
		}
		context.Next();
		return new FloatExpr {
			location = token.location,
			value = value
		};
	}
	
	private static TypeExpr ParseVoidType(Token token)
	{
		return new TypeExpr {
			location = token.location,
			type = new MetaType { instanceType = new VoidType() }
		};
	}
	
	private static TypeExpr ParsePrimType(Token token)
	{
		return new TypeExpr {
			location = token.location,
			type = new MetaType { instanceType = new PrimType { kind = Constants.tokenToPrim[token.kind] } }
		};
	}

	private static UnaryExpr ParsePrefixExpr(Token token, Expr value)
	{
		return new UnaryExpr {
			location = token.location,
			op = token.kind.AsUnaryOp(),
			value = value
		};
	}
	
	private static BinaryExpr ParseBinaryExpr(Expr left, Token token, Expr right)
	{
		return new BinaryExpr {
			location = token.location,
			left = left,
			op = token.kind.AsBinaryOp(),
			right = right
		};
	}
	
	private static CastExpr ParseCastExpr(ParserContext context, Expr left)
	{
		// Create the node
		CastExpr node = new CastExpr {
			location = context.CurrentToken().location,
			value = left
		};
		context.Next();
		
		// Parse the target type
		node.target = pratt.Parse(context, Constants.operatorPrecedence[TokenKind.As]);
		if (node.target == null) {
			return null;
		}
		
		return node;
	}
	
	private static MemberExpr ParseMemberExpr(ParserContext context, Expr left)
	{
		// Create the node
		MemberExpr node = new MemberExpr {
			location = context.CurrentToken().location,
			obj = left
		};
		context.Next();
		
		// Parse the member identifier
		node.name = context.CurrentToken().text;
		if (!context.Consume(TokenKind.Identifier)) {
			return null;
		}
		
		return node;
	}
	
	private static Expr ParseGroup(ParserContext context)
	{
		// A group is an expression wrapped in parentheses
		context.Next();
		Expr node = pratt.Parse(context);
		if (node == null || !context.Consume(TokenKind.RParen)) {
			return null;
		}
		return node;
	}
	
	private static CallExpr ParseCallExpr(ParserContext context, Expr left)
	{
		// Create the node
		CallExpr node = new CallExpr {
			location = context.CurrentToken().location,
			func = left,
			args = new List<Expr>()
		};
		context.Next();
		
		// Parse the argument list
		bool first = true;
		while (!context.Consume(TokenKind.RParen)) {
			if (first) {
				first = false;
			} else if (!context.Consume(TokenKind.Comma)) {
				return null;
			}
			Expr arg = pratt.Parse(context);
			if (arg == null) {
				return null;
			}
			node.args.Add(arg);
		}
		
		return node;
	}
	
	private static bool ParseEndOfStatement(ParserContext context)
	{
		return context.Consume(TokenKind.Semicolon) || context.Consume(TokenKind.Newline) ||
			context.Peek(TokenKind.RBrace) || context.Peek(TokenKind.EndOfFile);
	}
	
	private static Stmt ParseStmt(ParserContext context, Block block)
	{
		// Try to parse identifiable statements (ones that don't start with an expression)
		if (context.Peek(TokenKind.If)) {
			return ParseIfStmt(context);
		}
		if (context.Peek(TokenKind.Return)) {
			return ParseReturnStmt(context);
		}
		if (context.Peek(TokenKind.External)) {
			return ParseExternalStmt(context);
		}
		if (context.Peek(TokenKind.Class)) {
			return ParseClassDef(context);
		}
		
		// Check for modifiers now
		bool isStatic = context.Consume(TokenKind.Static);
		
		// If we don't know what it is yet, try an expression
		Location location = context.CurrentToken().location;
		Expr expr = pratt.Parse(context);
		if (expr == null) {
			return null;
		}
		
		// Check for end of statement (then it's a free expression)
		if (ParseEndOfStatement(context)) {
			return new ExprStmt {
				location = location,
				value = expr
			};
		}
		location = context.CurrentToken().location;
		
		// Assume we have a definition (will start with a name)
		string name = context.CurrentToken().text;
		if (!context.Consume(TokenKind.Identifier)) {
			return null;
		}
		
		// Function definition
		if (context.Consume(TokenKind.LParen)) {
			FuncDef func = new FuncDef {
				location = location,
				name = name,
				isStatic = isStatic,
				returnType = expr,
				argDefs = new List<VarDef>()
			};
			
			// Parse arguments
			bool first = true;
			while (!context.Consume(TokenKind.RParen)) {
				if (first) {
					first = false;
				} else if (!context.Consume(TokenKind.Comma)) {
					return null;
				}
				VarDef arg = new VarDef { location = context.CurrentToken().location };
				if ((arg.type = pratt.Parse(context)) == null) {
					return null;
				}
				arg.name = context.CurrentToken().text;
				if (!context.Consume(TokenKind.Identifier)) {
					return null;
				}
				if (context.Consume(TokenKind.Assign) && (arg.value = pratt.Parse(context)) == null) {
					return null;
				}
				func.argDefs.Add(arg);
			}
			
			// Parse the block
			if (!ParseEndOfStatement(context) && ((func.block = ParseBlock(context)) == null || !ParseEndOfStatement(context))) {
				return null;
			}
			
			return func;
		}
		
		// Only possible option is a variable definition
		if (isStatic) {
			return null;
		}
		
		// Variable definition and initialization
		VarDef node = new VarDef {
			location = location,
			name = name,
			type = expr
		};
		if (context.Consume(TokenKind.Assign) && (node.value = pratt.Parse(context)) == null) {
			return null;
		}
		
		// Check for additional variables and add them to the current block as
		// separate VarDef statements (returning just the last one)
		while (context.Consume(TokenKind.Comma)) {
			block.stmts.Add(node);
			node = new VarDef {
				location = context.CurrentToken().location,
				name = context.CurrentToken().text,
				type = expr
			};
			if (!context.Consume(TokenKind.Identifier)) {
				return null;
			}
			if (context.Consume(TokenKind.Assign) && (node.value = pratt.Parse(context)) == null) {
				return null;
			}
		}
		
		// Check for end of statement
		if (!ParseEndOfStatement(context)) {
			return null;
		}
		
		return node;
	}
	
	private static IfStmt ParseIfStmt(ParserContext context)
	{
		// Create the node
		IfStmt node = new IfStmt { location = context.CurrentToken().location };
		context.Next();
		
		// Parse the if statement
		if ((node.test = pratt.Parse(context)) == null || (node.thenBlock = ParseBlock(context)) == null) {
			return null;
		}
		
		// A newline is optional after an if and before an else
		bool endOfStatement = ParseEndOfStatement(context);
		
		// Check for an else block (we don't have dangling else problems since we require braces around blocks)
		if (context.Consume(TokenKind.Else)) {
			// Special-case else if
			if (context.Peek(TokenKind.If)) {
				Stmt elseIf = ParseIfStmt(context);
				if (elseIf == null) {
					return null;
				}
				node.elseBlock = new Block {
					location = context.CurrentToken().location,
					stmts = new List<Stmt> { elseIf }
				};
				return node;
			}
			
			// Otherwise just parse an else block and forget the end of the statement because we've read more since then
			endOfStatement = false;
			if ((node.elseBlock = ParseBlock(context)) == null) {
				return null;
			}
		}
		
		// Check for end of statement
		if (!endOfStatement && !ParseEndOfStatement(context)) {
			return null;
		}
		
		return node;
	}
	
	private static ReturnStmt ParseReturnStmt(ParserContext context)
	{
		// Create the node
		ReturnStmt node = new ReturnStmt { location = context.CurrentToken().location };
		context.Next();
		
		// First check for a void return
		if (ParseEndOfStatement(context)) {
			return node;
		}
		
		// Otherwise there must be an expression
		if ((node.value = pratt.Parse(context)) == null || !ParseEndOfStatement(context)) {
			return null;
		}
		
		return node;
	}
	
	private static ExternalStmt ParseExternalStmt(ParserContext context)
	{
		// Create the node
		ExternalStmt node = new ExternalStmt { location = context.CurrentToken().location };
		context.Next();
		
		// Parse the block
		if ((node.block = ParseBlock(context)) == null) {
			return null;
		}
		
		// Check for end of statement
		if (!ParseEndOfStatement(context)) {
			return null;
		}
		
		return node;
	}
	
	private static ClassDef ParseClassDef(ParserContext context)
	{
		// Create the node
		ClassDef node = new ClassDef { location = context.CurrentToken().location };
		context.Next();
		
		// Parse the name
		node.name = context.CurrentToken().text;
		if (!context.Consume(TokenKind.Identifier)) {
			return null;
		}
		
		// Parse the block
		if ((node.block = ParseBlock(context)) == null) {
			return null;
		}
		
		// Check for end of statement
		if (!ParseEndOfStatement(context)) {
			return null;
		}
		
		return node;
	}
	
	private static Block ParseBlock(ParserContext context)
	{
		// Create the node
		Block node = new Block {
			location = context.CurrentToken().location
		};
		
		// Read the opening brace, swallowing up to one newline on either side
		context.Consume(TokenKind.Newline);
		if (!context.Consume(TokenKind.LBrace)) {
			return null;
		}
		context.Consume(TokenKind.Newline);
		
		// Keep reading values until the closing brace
		node.stmts = new List<Stmt>();
		while (!context.Consume(TokenKind.RBrace)) {
			Stmt stmt = ParseStmt(context, node);
			if (stmt == null) {
				return null;
			}
			node.stmts.Add(stmt);
		}
		
		return node;
	}
	
	private static Module ParseModule(ParserContext context, string name)
	{
		// Create the node
		Module node = new Module {
			location = context.CurrentToken().location,
			name = name,
			block = new Block {
				stmts = new List<Stmt>()
			}
		};
		
		// Keep reading statements until the end of the file
		context.Consume(TokenKind.Newline);
		while (!context.Consume(TokenKind.EndOfFile)) {
			Stmt stmt = ParseStmt(context, node.block);
			if (stmt == null) {
				return null;
			}
			node.block.stmts.Add(stmt);
		}
		
		return node;
	}
	
	public static Module Parse(Log log, List<Token> tokens, string moduleName)
	{
		// Run the parser over the input
		ParserContext context = new ParserContext(tokens);
		Module node = ParseModule(context, moduleName);
		
		// Check that we parsed everything
		if (node != null) {
			return node;
		}
		
		// Assume the current token is the location of the error
		log.Error(context.CurrentToken().location, "unexpected " + context.CurrentToken().ErrorText());
		return null;
	}
}
