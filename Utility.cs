using System;
using System.Collections;
using System.Collections.Generic;

public class Log
{
	public List<string> warnings = new List<string>();
	public List<string> errors = new List<string>();
	
	public void Warning(Location location, string text)
	{
		warnings.Add(Location.Where(location) + ": warning: " + text);
	}
	
	public void Error(Location location, string text)
	{
		errors.Add(Location.Where(location) + ": error: " + text);
	}
}

public class OperatorInfo<T>
{
	public TokenKind kind;
	public int precedence;
	public T op;
}

public class OperatorList<T> : IEnumerable
{
	public List<OperatorInfo<T>> list = new List<OperatorInfo<T>>();
	public Dictionary<TokenKind, T> kindToEnum = new Dictionary<TokenKind, T>();
	public Dictionary<T, TokenKind> enumToKind = new Dictionary<T, TokenKind>();
	
	public IEnumerator GetEnumerator()
	{
		return list.GetEnumerator();
	}
	
	public void Add(TokenKind kind, int precedence, T op)
	{
		list.Add(new OperatorInfo<T> { kind = kind, precedence = precedence, op = op });
 		kindToEnum[kind] = op;
		enumToKind[op] = kind;
	}
}

public static class Constants
{
	// Table of binary operators mapping TokenKind <=> BinaryOp
	public static readonly OperatorList<BinaryOp> binaryOperators = new OperatorList<BinaryOp> {
		{ TokenKind.Assign, 1, BinaryOp.Assign },
		
		{ TokenKind.And, 2, BinaryOp.And },
		{ TokenKind.Or, 2, BinaryOp.Or },
		
		{ TokenKind.Equal, 3, BinaryOp.Equal },
		{ TokenKind.NotEqual, 3, BinaryOp.NotEqual },
		{ TokenKind.LessThan, 3, BinaryOp.LessThan },
		{ TokenKind.GreaterThan, 3, BinaryOp.GreaterThan },
		{ TokenKind.LessThanEqual, 3, BinaryOp.LessThanEqual },
		{ TokenKind.GreaterThanEqual, 3, BinaryOp.GreaterThanEqual },
		
		{ TokenKind.Add, 4, BinaryOp.Add },
		{ TokenKind.Subtract, 4, BinaryOp.Subtract },
		
		{ TokenKind.Multiply, 5, BinaryOp.Multiply },
		{ TokenKind.Divide, 5, BinaryOp.Divide },
	};
	
	// Table of unary operators mapping TokenKind <=> UnaryOp
	public static readonly OperatorList<UnaryOp> unaryOperators = new OperatorList<UnaryOp> {
		{ TokenKind.Subtract, 7, UnaryOp.Negative },
		{ TokenKind.Not, 7, UnaryOp.Not },
	};
	
	// Operator precedence table for operators not in binaryOperators and unaryOperators
	public static readonly Dictionary<TokenKind, int> operatorPrecedence = new Dictionary<TokenKind, int> {
		{ TokenKind.As, 6 },
		{ TokenKind.Dot, 8 },
		{ TokenKind.LParen, 8 },
	};
	
	// Map all symbols, operators, and keywords to the equivalent TokenKind
	public static readonly Dictionary<string, TokenKind> stringToKind = new Dictionary<string, TokenKind> {
		{ "(", TokenKind.LParen },
		{ ")", TokenKind.RParen },
		{ "[", TokenKind.LBracket },
		{ "]", TokenKind.RBracket },
		{ "{", TokenKind.LBrace },
		{ "}", TokenKind.RBrace },
		{ ",", TokenKind.Comma },
		{ ";", TokenKind.Semicolon },
		{ ".", TokenKind.Dot },
		{ ":", TokenKind.Colon },
		{ "\\", TokenKind.Backslash },
		
		{ "+", TokenKind.Add },
		{ "-", TokenKind.Subtract },
		{ "*", TokenKind.Multiply },
		{ "/", TokenKind.Divide },
		{ "==", TokenKind.Equal },
		{ "!=", TokenKind.NotEqual },
		{ "<=", TokenKind.LessThanEqual },
		{ ">=", TokenKind.GreaterThanEqual },
		{ "<", TokenKind.LessThan },
		{ ">", TokenKind.GreaterThan },
		{ "=", TokenKind.Assign },
		
		{ "if", TokenKind.If },
		{ "else", TokenKind.Else },
		{ "class", TokenKind.Class },
		{ "return", TokenKind.Return },
		{ "and", TokenKind.And },
		{ "or", TokenKind.Or },
		{ "not", TokenKind.Not },
		{ "as", TokenKind.As },
		{ "external", TokenKind.External },
		{ "null", TokenKind.Null },
		{ "true", TokenKind.True },
		{ "false", TokenKind.False },
		{ "void", TokenKind.Void },
		{ "bool", TokenKind.Bool },
		{ "int", TokenKind.Int },
		{ "float", TokenKind.Float },
		{ "string", TokenKind.String },
	};
	
	// The inverse mapping of stringToKind
	public static readonly Dictionary<TokenKind, string> kindToString = stringToKind.Inverse();
}

public static class Utility
{
	public static string Quote(int c, char quote)
	{
		if (c == quote) {
			return "\\" + quote;
		}
		switch (c) {
			case '\t':
				return "\\t";
			case '\r':
				return "\\r";
			case '\n':
				return "\\n";
			case '\\':
				return "\\\\";
		}
		if (c >= 0x20 && c <= 0x7E) {
			return ((char)c).ToString();
		}
		if (c >= 0x00 && c <= 0xFF) {
			return "\\x" + string.Format("{0:X}", c).PadLeft(2, '0');
		}
		return "\\u" + string.Format("{0:X}", c).PadLeft(4, '0');
	}
	
	public static string ToQuotedString(this string text)
	{
		List<char> chars = new List<char>(text.ToCharArray());
		return "\"" + string.Join("", chars.ConvertAll(x => Quote(x, '"')).ToArray()) + "\"";
	}
	
	public static string ToQuotedChar(this int c)
	{
		return "'" + Quote(c, '\'') + "'";
	}
	
	public static Dictionary<V, K> Inverse<K, V>(this Dictionary<K, V> dict)
	{
		Dictionary<V, K> inverse = new Dictionary<V, K>();
		foreach (KeyValuePair<K, V> pair in dict)
			inverse[pair.Value] = pair.Key;
		return inverse;
	}
	
	public static UnaryOp AsUnaryOp(this TokenKind kind)
	{
		return Constants.unaryOperators.kindToEnum[kind];
	}
	
	public static BinaryOp AsBinaryOp(this TokenKind kind)
	{
		return Constants.binaryOperators.kindToEnum[kind];
	}
	
	public static string AsString(this UnaryOp op)
	{
		return Constants.kindToString[Constants.unaryOperators.enumToKind[op]];
	}
	
	public static string AsString(this BinaryOp op)
	{
		return Constants.kindToString[Constants.binaryOperators.enumToKind[op]];
	}
}
