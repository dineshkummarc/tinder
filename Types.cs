using System;
using System.Collections.Generic;

public abstract class Type
{
	public abstract bool EqualsType(Type other);
}

public class VoidType : Type
{
	public override bool EqualsType(Type other)
	{
		return other is VoidType;
	}
	
	public override string ToString()
	{
		return "void";
	}
}

public enum PrimKind
{
	Bool,
	Int,
	Float,
	Char,
	String,
}

public class PrimType : Type
{
	public PrimKind kind;
	
	public override bool EqualsType(Type other)
	{
		return other is PrimType && kind == ((PrimType)other).kind;
	}
	
	public override string ToString()
	{
		return Constants.tokenToString[Constants.primToToken[kind]];
	}
}

public class FuncType : Type
{
	public Type returnType;
	public List<Type> argTypes;
	
	public override bool EqualsType(Type other)
	{
		return other is FuncType && returnType.EqualsType((FuncType)other) && argTypes.Matches(((FuncType)other).argTypes);
	}
	
	public override string ToString()
	{
		return returnType + " function(" + string.Join(", ", argTypes.ConvertAll(x => x.ToString()).ToArray()) + ")";
	}
}

public class ClassType : Type
{
	public ClassDef def;
	
	public override bool EqualsType(Type other)
	{
		return other is ClassType && def == ((ClassType)other).def;
	}
	
	public override string ToString()
	{
		return def.name;
	}
}

public class MetaType : Type
{
	public Type instanceType;
	
	public override bool EqualsType(Type other)
	{
		return other is MetaType && instanceType.EqualsType(((MetaType)other).instanceType);
	}
	
	public override string ToString()
	{
		return "type " + instanceType;
	}
}
