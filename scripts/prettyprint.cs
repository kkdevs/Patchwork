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
	public static List<string> ignore = new List<string>() { "ChaControl" };
	public static Dictionary<Symbol, object> obj2dict(object o, string pfx)
	{
		if (o == null)
			return null;
		var dict = new Dictionary<Symbol, object>();
		var t = o.GetType();
		if (t == null)
			return null;
		foreach (var f in t.GetFields())
//			if (!ignore.Contains(f.FieldType.Name))
				dict[new Symbol() { s = f.Name } ] = f.GetValue(o);
		var props = t.GetProperties();
		if (t != null && pfx.Length < maxDepth)
			foreach (var p in props)
			{
//				if (ignore.Contains(p.PropertyType.Name))
//					continue;
				object val = null;
				try
				{
					if (p.CanRead)
						val = p.GetValue(o, null);
				} catch (System.Exception ex)
				{
					val = (object)ex.Message;
				}
				var ds = new Symbol() { s = p.Name };
				dict[ds] = val;
			}	
		return dict;
	}
	public static int maxDepth = 5;
	public static bool dangerousType(object o, string pfx)
	{
		//if (o is Object)
		//	return true;
		return false;
	}
	public static string PrettyPrint(IEnumerable o, string pfx, bool isprim = false)
	{
		HideFlags x;
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
	public static bool showChildren = false;
	public static string PrettyPrint(object o, string prefix = "")
	{
		if (o == null)
			return "null";
		if (o is Symbol)
			return (o as Symbol).s;
		if ((o is string) || (o is System.String))
			return $"\"{o}\"";
		if (o is char)
			return $"'{o}'";
		var t = o.GetType();
		if (ignore.Contains(t.Name))
			return "<ignore>";
		if (t.IsPrimitive || t.IsEnum)
			return o.ToString();
		if (cycleRefs.Contains(o))
			return "<cycle>";
		if (prefix.Length > maxDepth)
			return "...";
		cycleRefs.Add(o);
		if (o is IDictionary)
			return PrettyPrint(o as IDictionary, prefix);
		var tmp = "";
		if (o is Component)
		{
			tmp = getname(o) + " " + PrettyPrint(obj2dict(o, prefix) as IDictionary, prefix);
			if (!(o is IEnumerable))
				return tmp;
			tmp += ",\n"+prefix;
		}
		if (o is IEnumerable)
		{
			if (tmp != "")
			{ // todo: maybe detect count first?
				if (!showChildren)
					return tmp;
				tmp += "^Children = ";
			}
			var prim = false;
			var et = t.GetElementType();
			var ga = t.GetGenericArguments();
			if (et != null && et.IsPrimitive)
				prim = true;
			if (ga != null && ga.Length > 0 && ga[0].IsPrimitive)
				prim = true;
			return tmp + PrettyPrint(o as IEnumerable, prefix, prim);
		}
		if (tmp != "")
			return tmp;
		if (o is GameObject)
			return getname(o) + " " + PrettyPrint(obj2dict(o, prefix) as IDictionary, prefix);
		if (t.IsNested)
		{
			if (typeof(GameObject).IsAssignableFrom(t.DeclaringType))
				return getname(o) + " " + PrettyPrint(obj2dict(o, prefix) as IDictionary, prefix);
			if (typeof(Component).IsAssignableFrom(t.DeclaringType))
				return getname(o) + " " + PrettyPrint(obj2dict(o, prefix) as IDictionary, prefix);
		}


		// Has a native (not a base object) tostring()?
		MethodInfo tos = null;
		try
		{
			// can be ambig
			tos = t.GetMethod("ToString", new System.Type[] { });
		}
		catch { };
		if (tos != null)
			return tos.Invoke(o, null) as string;
		return t.FullName;
	}

	public static string getname(object o)
	{
		if (o == null)
			return "null";
		var ot = o.GetType();
		var nf = ot.GetField("name") ?? ot.GetField("Name");
		string name = null;
		if (nf != null)
		{
			try
			{
				name = nf.GetValue(o) as string;
			}
			catch { };
		} else
		{
			var np = ot.GetProperty("name") ?? ot.GetProperty("Name");
			try
			{
				name = np.GetValue(o, null) as string;
			}
			catch { };
		}
		if (name.IsNullOrEmpty())
			return ot.Name;
		return ot.Name + "#" + name;
	}

	public static HashSet<object> cycleRefs = new HashSet<object>();
	public static new void pp(object o)
	{
		cycleRefs.Clear();
		print(PrettyPrint(o));
	}
}
