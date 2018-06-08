using Patchwork;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using Illusion.Extensions;

public class Symbol
{
	public string s;
}

public partial class ScriptEnv : Script
{
	public static Dictionary<Symbol, object> obj2dict(object o)
	{
		var dict = new Dictionary<Symbol, object>();
		var t = o.GetType();
		foreach (var f in t.GetFields())
			dict[new Symbol() { s = f.Name } ] = f.GetValue(o);
		return dict;
	}
	public static string PrettyPrint(IEnumerable o, string pfx, bool isprim = false)
	{
		string postfix = isprim ? "" : "\n";
		var sb = new StringBuilder();
		sb.Append("{ " + postfix);
		var npfx = isprim ? "" : pfx + " ";
		foreach (var item in o)
			sb.Append(npfx + PrettyPrint(item, npfx) + ", " + postfix);
		if (isprim)
			sb.Append("}");
		else
			sb.Append(pfx + "}");
		return sb.ToString();
	}
	public static string PrettyPrint(IDictionary o, string pfx)
	{
		var sb = new StringBuilder();
		sb.Append("{\n");
		foreach (DictionaryEntry item in o)
			sb.Append(pfx + " " + PrettyPrint(item.Key, pfx + " ") + " = " + PrettyPrint(item.Value, pfx + " ") + ",\n");
		sb.Append(pfx + "}");
		return sb.ToString();
	}
	public static string PrettyPrint(object o, string prefix = "")
	{
		if (o == null)
			return "null";
		if (o is Symbol)
			return (o as Symbol).s;
		if (o is string)
			return $"\"{o}\"";
		if (o is char)
			return $"'{o}'";
		var t = o.GetType();
		if (t.IsPrimitive)
			return o.ToString();
		if (o is IDictionary)
			return PrettyPrint(o as IDictionary, prefix);
		if (o is IEnumerable)
		{
			var prim = false;
			var et = t.GetElementType();
			var ga = t.GetGenericArguments();
			if (et != null && et.IsPrimitive)
				prim = true;
			if (ga != null && ga.Length > 0 && ga[0].IsPrimitive)
				prim = true;
			return prefix + PrettyPrint(o as IEnumerable, prefix, prim);
		}
		if (o is Transform)
			return PrettyPrint(obj2dict(o) as IDictionary, prefix);
		if (o is Component)
			return PrettyPrint(obj2dict(o) as IDictionary, prefix);

		// Has a native (not a base object) tostring()?
		MethodInfo tos = null;
		try
		{
			tos = t.GetMethod("ToString", new System.Type[] { });
		}
		catch { };
		if (tos != null)
			return tos.Invoke(o, null) as string;
		return t.FullName;
	}

	public static new void pp(object o)
	{
		print(PrettyPrint(o));
	}
}
