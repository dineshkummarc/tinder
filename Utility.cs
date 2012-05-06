using System;
using System.Collections;
using System.Collections.Generic;

public class Log
{
	public List<string> warnings = new List<string>();
	public List<string> errors = new List<string>();
	public bool disabled;
	
	public void Warning(Location location, string text)
	{
		if (!disabled) {
			warnings.Add(Location.Where(location) + ": warning: " + text);
		}
	}
	
	public void Error(Location location, string text)
	{
		if (!disabled) {
			errors.Add(Location.Where(location) + ": error: " + text);
		}
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
	public Dictionary<TokenKind, T> tokenToEnum = new Dictionary<TokenKind, T>();
	public Dictionary<T, TokenKind> enumToToken = new Dictionary<T, TokenKind>();
	
	public IEnumerator GetEnumerator()
	{
		return list.GetEnumerator();
	}
	
	public void Add(TokenKind kind, int precedence, T op)
	{
		list.Add(new OperatorInfo<T> { kind = kind, precedence = precedence, op = op });
		tokenToEnum[kind] = op;
		enumToToken[op] = kind;
	}
}

public static class Constants
{
	// Table of binary operators mapping TokenKind <=> BinaryOp
	public static readonly OperatorList<BinaryOp> binaryOperators = new OperatorList<BinaryOp> {
		{ TokenKind.Assign, 1, BinaryOp.Assign },
		
		{ TokenKind.NullableDefault, 2, BinaryOp.NullableDefault },
		
		{ TokenKind.And, 3, BinaryOp.And },
		{ TokenKind.Or, 3, BinaryOp.Or },
		
		{ TokenKind.Equal, 4, BinaryOp.Equal },
		{ TokenKind.NotEqual, 4, BinaryOp.NotEqual },
		
		{ TokenKind.LessThan, 5, BinaryOp.LessThan },
		{ TokenKind.GreaterThan, 5, BinaryOp.GreaterThan },
		{ TokenKind.LessThanEqual, 5, BinaryOp.LessThanEqual },
		{ TokenKind.GreaterThanEqual, 5, BinaryOp.GreaterThanEqual },
		
		{ TokenKind.LShift, 6, BinaryOp.LShift },
		{ TokenKind.RShift, 6, BinaryOp.RShift },
		{ TokenKind.BitAnd, 6, BinaryOp.BitAnd },
		{ TokenKind.BitOr, 6, BinaryOp.BitOr },
		{ TokenKind.BitXor, 6, BinaryOp.BitXor },
		
		{ TokenKind.Add, 7, BinaryOp.Add },
		{ TokenKind.Subtract, 7, BinaryOp.Subtract },
		
		{ TokenKind.Multiply, 8, BinaryOp.Multiply },
		{ TokenKind.Divide, 8, BinaryOp.Divide },
	};
	
	// Table of unary operators mapping TokenKind <=> UnaryOp
	public static readonly OperatorList<UnaryOp> unaryOperators = new OperatorList<UnaryOp> {
		{ TokenKind.Subtract, 10, UnaryOp.Negative },
		{ TokenKind.Not, 10, UnaryOp.Not },
	};
	
	// Operator precedence table for operators not in binaryOperators and unaryOperators
	public static readonly Dictionary<TokenKind, int> operatorPrecedence = new Dictionary<TokenKind, int> {
		{ TokenKind.As, 9 },
		{ TokenKind.Dot, 11 },
		{ TokenKind.LParen, 11 },
		{ TokenKind.LParam, 11 },
		{ TokenKind.LBracket, 11 },
		{ TokenKind.Nullable, 11 },
		{ TokenKind.NullableDot, 11 },
	};
	
	// Map all symbols, operators, and keywords to the equivalent TokenKind
	public static readonly Dictionary<string, TokenKind> stringToToken = new Dictionary<string, TokenKind> {
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
		
		{ "=", TokenKind.Assign },
		{ "+", TokenKind.Add },
		{ "-", TokenKind.Subtract },
		{ "*", TokenKind.Multiply },
		{ "/", TokenKind.Divide },
		{ "<<", TokenKind.LShift },
		{ ">>", TokenKind.RShift },
		{ "&", TokenKind.BitAnd },
		{ "|", TokenKind.BitOr },
		{ "^", TokenKind.BitXor },
		{ "==", TokenKind.Equal },
		{ "!=", TokenKind.NotEqual },
		{ "<=", TokenKind.LessThanEqual },
		{ ">=", TokenKind.GreaterThanEqual },
		{ "<", TokenKind.LessThan },
		{ ">", TokenKind.GreaterThan },
		{ "?.", TokenKind.NullableDot },
		{ "??", TokenKind.NullableDefault },
		{ "?", TokenKind.Nullable },
		
		{ "if", TokenKind.If },
		{ "else", TokenKind.Else },
		{ "while", TokenKind.While },
		{ "class", TokenKind.Class },
		{ "return", TokenKind.Return },
		{ "and", TokenKind.And },
		{ "or", TokenKind.Or },
		{ "not", TokenKind.Not },
		{ "as", TokenKind.As },
		{ "external", TokenKind.External },
		{ "static", TokenKind.Static },
		{ "var", TokenKind.Var },
		{ "null", TokenKind.Null },
		{ "this", TokenKind.This },
		{ "true", TokenKind.True },
		{ "false", TokenKind.False },
		{ "void", TokenKind.Void },
		{ "bool", TokenKind.Bool },
		{ "int", TokenKind.Int },
		{ "float", TokenKind.Float },
		{ "string", TokenKind.String },
		{ "list", TokenKind.List },
		{ "function", TokenKind.Function },
	};
	
	// Map tokens for primitive types to the equivalent PrimKind
	public static readonly Dictionary<TokenKind, PrimKind> tokenToPrim = new Dictionary<TokenKind, PrimKind> {
		{ TokenKind.Bool, PrimKind.Bool },
		{ TokenKind.Int, PrimKind.Int },
		{ TokenKind.Float, PrimKind.Float },
		{ TokenKind.String, PrimKind.String },
	};
	
	// Inverse mappings
	public static readonly Dictionary<TokenKind, string> tokenToString = stringToToken.Inverse();
	public static readonly Dictionary<PrimKind, TokenKind> primToToken = tokenToPrim.Inverse();
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
		return "\"" + chars.ConvertAll(x => Quote(x, '"')).Join() + "\"";
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
	
	public static List<KeyValuePair<K, V>> Items<K, V>(this Dictionary<K, V> dict)
	{
		List<KeyValuePair<K, V>> pairs = new List<KeyValuePair<K, V>>();
		foreach (KeyValuePair<K, V> pair in dict)
			pairs.Add(pair);
		return pairs;
	}
	
	public static UnaryOp AsUnaryOp(this TokenKind kind)
	{
		return Constants.unaryOperators.tokenToEnum[kind];
	}
	
	public static BinaryOp AsBinaryOp(this TokenKind kind)
	{
		return Constants.binaryOperators.tokenToEnum[kind];
	}
	
	public static string AsString(this UnaryOp op)
	{
		return Constants.tokenToString[Constants.unaryOperators.enumToToken[op]];
	}
	
	public static string AsString(this BinaryOp op)
	{
		return Constants.tokenToString[Constants.binaryOperators.enumToToken[op]];
	}
	
	public static bool CanImplicitlyConvertTo(this Type from, Type to)
	{
		if (from.IsInt() && to.IsFloat()) {
			return true;
		}
		if (to is NullableType) {
			Type type = ((NullableType)to).type;
			if (from is NullType || from.EqualsType(type) || from.CanImplicitlyConvertTo(type)) {
				return true;
			}
		}
		return false;
	}
	
	public static bool MatchesExactly(this List<Type> a, List<Type> b)
	{
		if (a.Count != b.Count) {
			return false;
		}
		for (int i = 0; i < a.Count; i++) {
			if (!a[i].EqualsType(b[i])) {
				return false;
			}
		}
		return true;
	}
	
	public static bool MatchesWithImplicitConversions(this List<Type> from, List<Type> to)
	{
		if (from.Count != to.Count) {
			return false;
		}
		for (int i = 0; i < from.Count; i++) {
			if (!from[i].EqualsType(to[i]) && !from[i].CanImplicitlyConvertTo(to[i])) {
				return false;
			}
		}
		return true;
	}
	
	public static string Join(this List<Type> argTypes)
	{
		return argTypes.ConvertAll(arg => arg.ToString()).Join(", ");
	}
	
	public static bool IsBool(this Type type)
	{
		return type is PrimType && ((PrimType)type).kind == PrimKind.Bool;
	}
	
	public static bool IsInt(this Type type)
	{
		return type is PrimType && ((PrimType)type).kind == PrimKind.Int;
	}
	
	public static bool IsFloat(this Type type)
	{
		return type is PrimType && ((PrimType)type).kind == PrimKind.Float;
	}
	
	public static bool IsString(this Type type)
	{
		return type is PrimType && ((PrimType)type).kind == PrimKind.String;
	}
	
	public static bool IsNumeric(this Type type)
	{
		return type.IsInt() || type.IsFloat();
	}
	
	public static bool HasFreeParams(this Type type)
	{
		return (type is ListType && type.ItemType() == null) || (type is FuncType && type.ReturnType() == null);
	}
	
	public static bool IsCompleteType(this Type type)
	{
		return type is MetaType && !type.InstanceType().HasFreeParams();
	}
	
	public static Type ItemType(this Type type)
	{
		return ((ListType)type).itemType;
	}
	
	public static Type InstanceType(this Type type)
	{
		return ((MetaType)type).instanceType;
	}
	
	public static Type ReturnType(this Type type)
	{
		return ((FuncType)type).returnType;
	}
	
	public static List<Type> ArgTypes(this Type type)
	{
		return ((FuncType)type).argTypes;
	}
	
	public static NullableType AsNullableType(this Type type)
	{
		// Don't nest more than one level of nullable types
		if (type is NullableType) {
			return (NullableType)type;
		}
		return new NullableType { type = type };
	}
	
	public static string StripParens(this string text)
	{
		return text.StartsWith("(") && text.EndsWith(")") ? text.Substring(1, text.Length - 2) : text;
	}
	
	public static string Join(this List<string> list, string separator = "")
	{
		return string.Join(separator, list.ToArray());
	}
}
