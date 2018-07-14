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
using static Patchwork;

// We frequently re-parent game types to those, hence the global NS
public interface IDumpable
{
	bool Unmarshal(string src, string ext, string name, string path);
	string Marshal();
	string GetFileExt();
}

public class JSONMarshaller : ScriptableObject
{
	public string GetFileExt()
	{
		return "json";
	}
	public bool Unmarshal(string src, string ext, string name, string path)
	{
		fsData data = fsJsonParser.Parse(src);
		var self = this;
		fsJson.TryDeserialize(data, ref self).AssertSuccess();
		return self == this;
	}
	public static fsSerializer fsJson = new fsSerializer();
	public string Marshal()
	{
		fsData data = null;
		fsJson.TrySerialize(this, out data).AssertSuccess();
		return fsJsonPrinter.PrettyJson(data);
	}
}

public class CSVMarshaller : ScriptableObject
{
	public string GetFileExt()
	{
		return "csv";
	}
	// locate the list element
	private FieldInfo getParam()
	{
		foreach (var fi in this.GetType().GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance))
			if (fi.FieldType.IsList())
				return fi;
		return null;
	}

	public bool Unmarshal(string src, string ext, string name, string path)
	{
		var tlist = getParam();
		var param = tlist.FieldType.GetGenericArguments()[0];
		var add = tlist.FieldType.GetMethod("Add");
		var listref = tlist.GetValue(this);
		var first = true;
		foreach (var row in CSV.Parse(src, ext))
		{
			// Skip header for anything but excel
			if (first && (!(this is ExcelData)))
			{
				first = false;
				continue;
			}
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

	public string Marshal()
	{
		return CSV.ToString(MarshalEnum());
	}

	public IEnumerable<IEnumerable<string>> MarshalEnum()
	{
		var list = getParam();
		var row = new List<string>();
		var first = true;
		foreach (var item in (IEnumerable)list.GetValue(this))
		{
			row.Clear();
			if ((!(this is ExcelData)) && first)
			{
				foreach (var f in item.GetType().GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance))
				{
					row.Add(f.Name);
				}
				yield return row;
				first = false;
				row.Clear();
			}
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

public static class JSON
{
	public static fsSerializer json = new fsSerializer();
	public static string Serialize<T>(T o)
	{
		fsData data;
		json.TrySerialize(o, out data);
		return fsJsonPrinter.PrettyJson(data);
	}
	public static T Deserialize<T>(string s, T res = null) where T : class
	{
		var data = fsJsonParser.Parse(s);
		json.TryDeserialize(data, ref res);
		return res;
	}
}