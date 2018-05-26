using ParadoxNotion.Serialization.FullSerializer;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using Patchwork;

// We frequently re-parent game types to those, hence the global NS
interface IDumpable
{
	bool Unmarshal(IEnumerable<IEnumerable<string>> src);
	IEnumerable<IEnumerable<string>> Marshal();
}

public class GenericMarshaller : ScriptableObject
{
	// locate the list element
	private FieldInfo getParam()
	{
		foreach (var fi in this.GetType().GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance))
			if (fi.FieldType.IsList())
				return fi;
		return null;
	}

	public bool Unmarshal(IEnumerable<IEnumerable<string>> src)
	{
		var tlist = getParam();
		var param = tlist.FieldType.GetGenericArguments()[0];
		var add = tlist.FieldType.GetMethod("Add");
		var listref = tlist.GetValue(this);
		foreach (var row in src)
		{
			// XXX TODO: This usage of reflection is fairly slow. Check if we're not slowing something too much.
			var rowo = System.Activator.CreateInstance(param);
			var rowe = row.GetEnumerator();
			foreach (var f in param.GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance))
			{
				var str = rowe.Current;
				// XXX presumes either array or list of strings
				if (f.FieldType.IsArray || f.FieldType.IsList())
				{
					var strl = new List<string>();
					while (rowe.MoveNext())
						strl.Add(rowe.Current);
					f.SetValue(rowo, f.FieldType.IsList()?(object)strl:(object)strl.ToArray());
					break; // this is always last one
				}
				rowe.MoveNext();
				var conv = TypeDescriptor.GetConverter(f.FieldType);
				//f.SetValue(rowo, System.Convert.ChangeType(rowe.Current, f.FieldType));
				f.SetValue(rowo, conv.ConvertFromInvariantString(rowe.Current));
			}
			add.Invoke(listref, new[] { rowo });
		}
		return true;
	}

	public IEnumerable<IEnumerable<string>> Marshal()
	{
		var list = getParam();
		var row = new List<string>();
		foreach (var item in (IEnumerable)list.GetValue(this))
		{
			row.Clear();
			foreach (var f in item.GetType().GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance))
			{
				if (f.FieldType.IsArray || f.FieldType.IsList())
					foreach (var s in f.GetValue(item) as IEnumerable)
						row.Add(s as string);
				else
					row.Add(f.GetValue(item).ToString());
			}
			yield return row;
		}
	}
}

