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

		/*
		if (listref == null)
		{
			listref = Activator.CreateInstance(tlist.FieldType);
			tlist.SetValue(this, listref);
		}*/

		List<FieldInfo> fields = null;
		var flags = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance;
		int rownum = 0;
		foreach (var row in CSV.Parse(src, ext))
		{
			rownum++;
			// Skip header for anything but excel.
			if (fields == null)
			{
				fields = new List<FieldInfo>();
				if (!(this is ExcelData))
				{
					foreach (var col in row)
					{
						var fl = param.GetField(col, flags);
						if (fl == null)
							throw new Exception("unknown CSV field " + fl);
						fields.Add(fl);
					}
					continue;
				}
				// for excel, we have one explicit "column", and no heading, data follows immediately
				fields.Add(param.GetField("list", flags));
			}
			var rowo = System.Activator.CreateInstance(param);
			var rowe = row.GetEnumerator();
			foreach (var f in fields)
			{
				// XXX presumes either array or list of strings
				if (f.FieldType.IsArray || f.FieldType.IsList())
				{
					if (fields.Last() == f)
					{
						var strl = new List<string>();
						while (rowe.MoveNext())
							strl.Add(rowe.Current);
						// keep trimming the array until first non-empty string
						while (strl.Count > 0 && strl.Last() == "")
							strl.RemoveAt(strl.Count - 1);
						f.SetValue(rowo, f.FieldType.IsList() ? (object)strl : (object)strl.ToArray());
						break; // this is always last one
					} else
					{
						if (!rowe.MoveNext())
							break;
						try
						{
							var flist = rowe.Current==""?new float[0]:rowe.Current.Split(' ').Select(x => float.Parse(x)).ToArray();
							f.SetValue(rowo, flist);
						} catch (Exception ex)
						{
							Debug.Error("float.Parse",rowe.Current,ex);
						}
						continue;
					}
				}
				if (!rowe.MoveNext())
				{
					Debug.Error(src,":",rownum," is corrupted: row terminates prematurely; skipping");
					goto skipRow;
				}

				// XXX cache converter too?
				var conv = TypeDescriptor.GetConverter(f.FieldType);
				var cur = rowe.Current;

				if (f.FieldType == typeof(bool) || f.FieldType == typeof(System.Boolean))
				{
					if (cur == "1" || cur == "TRUE") cur = "True";
					else if (cur == "0" || cur == "FALSE") cur = "False";
				}
				try
				{
					f.SetValue(rowo, conv.ConvertFromInvariantString(cur));
				} catch
				{
					Debug.Error(src, ":", rownum, $"is corrupted, field value '{cur}' doesn't represent {f.DeclaringType}.{f.Name} of type {f.FieldType}, skipping row");
					while (rowe.MoveNext()) ;
					goto skipRow;
				}
			}
			add:;
			add.Invoke(listref, new[] { rowo });
			skipRow:;
		}
#if GAME_DEBUG
		fsGlobalConfig.SerializeDefaultValues = true;
		File.WriteAllText("debug/" + Path.GetFileNameWithoutExtension(name) + ".json", JSON.Serialize(this, true));
#endif
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
			var fields = item.GetType().GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
			foreach (var f in fields)
			{
				if (f.FieldType.IsArray || f.FieldType.IsList())
				{
					if (fields.Last() == f) {
						int ngot = 0;
						foreach (var s in f.GetValue(item) as IEnumerable)
						{
							ngot++;
							row.Add(s as string);
						}
						// empty terminator
						if (ngot == 0)
							row.Add("");
					} else
					{
						var tb = new List<string>();
						foreach (var s in f.GetValue(item) as float[])
							tb.Add(s.ToString());
						row.Add(string.Join(" ", tb.ToArray()));
					}
				}
				else
					row.Add(f.GetValue(item).ToString());
			}
			yield return row;
		}
	}
}

public class MultiArrayConverter<T> : fsConverter
{
	public override bool CanProcess(Type type)
	{
		return type == typeof(T[,]);
	}

	public override bool RequestInheritanceSupport(Type storageType)
	{
		return false;
	}

	public override fsResult TrySerialize(object instance, out fsData serialized, Type storageType)
	{
		var arr = instance as T[,];
		var arrc = new T[arr.GetLength(1)][];
		for (int i = 0; i < arrc.Length; i++)
		{
			arrc[i] = new T[arr.GetLength(0)];
			for (int j = 0; j < arr.GetLength(0); j++)
				arrc[i][j] = arr[i, j];
		}
		return JSON.json.TrySerialize(arrc, out serialized);
	}

	public override fsResult TryDeserialize(fsData data, ref object instance, Type storageType)
	{
		T[][] arrc = null;
		JSON.json.TryDeserialize(data, ref arrc);
		var arr = instance as T[,];
		for (int i = 0; i < arrc.Length; i++)
			for (int j = 0; j < arrc[0].Length; j++)
				arr[i, j] = arrc[i][j];
		return fsResult.Success;
	}
}


public static class JSON
{
	public static fsSerializer json;
	public static void Init()
	{
		json = new fsSerializer();
	}
	public static string Serialize<T>(T o, bool pretty = false)
	{
		fsData data;
		json.TrySerialize(o, out data).AssertSuccess();
		return pretty ? fsJsonPrinter.PrettyJson(data) : fsJsonPrinter.CompressedJson(data);
	}
	public static T Deserialize<T>(string s, T res = null) where T : class
	{
		var data = fsJsonParser.Parse(s);
		json.TryDeserialize(data, ref res).AssertSuccess();
		return res;
	}
}