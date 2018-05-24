using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Patchwork;
using UnityEngine;
using System.IO.IsolatedStorage;
using System.Reflection;
using System.Collections;
using System.ComponentModel;
using ParadoxNotion.Serialization.FullSerializer;

interface IDumpable
{
	bool Unmarshal(string v);
	string Marshal();
}

public class GenericMarshaller : ScriptableObject
{
	public bool Unmarshal(string v)
	{
		return fetchList(v);
	}
	public string Marshal()
	{
		return dumpList();
	}

	// locate the list element
	private FieldInfo getParam()
	{
		
		foreach (var fi in this.GetType().GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance))
		{
			var ft = fi.FieldType;
			if (!ft.IsGenericType)
				continue;
			if (ft.GetGenericTypeDefinition() != typeof(List<>))
				continue;
			return fi;
		}
		return null;
	}

	private bool fetchList(string src)
	{
		var tlist = getParam();
		var param = tlist.FieldType.GetGenericArguments()[0];
		var add = tlist.FieldType.GetMethod("Add");
		var listref = tlist.GetValue(this);
		foreach (var row in ExcelData.SplitAndEscape(src))
		{
			var rowo = System.Activator.CreateInstance(param);
			var rowe = row.GetEnumerator();
			foreach (var f in param.GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance))
			{
				var str = rowe.Current;
				// XXX presumes array of string
				if (f.FieldType.IsArray)
				{
					var strl = new List<string>();
					while (rowe.MoveNext())
						strl.Add(rowe.Current);
					f.SetValue(rowo, strl.ToArray());
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

	private string dumpList()
	{
		var list = getParam();
		//var param = list.FieldType.GetGenericArguments()[0];
		var sb = new StringBuilder();
		foreach (var item in (IEnumerable)list.GetValue(this))
		{
			var row = new List<string>();
			foreach (var f in item.GetType().GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance))
			{
				var strarr = (f.GetValue(item) as string[]);
				if (strarr != null)
				{
					foreach (var s in strarr)
						row.Add("\"" + s.Replace("\"", "\"\"") + "\"");
				} else
					row.Add("\"" + f.GetValue(item).ToString().Replace("\"", "\"\"") + "\"");
			}
			sb.Append(System.String.Join(",", row.ToArray()));
			sb.Append("\n");
		}
		return sb.ToString();
	}
}

public class Cache
{
	public static void DumpJSON<T>(string bundle, string asset, T v)
	{
		var tfolder = dumpdir + Path.ChangeExtension(bundle, null);
		var jsfile = tfolder + "/" + asset + ".json";
		try
		{
			Directory.CreateDirectory(tfolder);
		}
		catch { };

		fsData data = null;
		Program.json.TrySerialize(v, out data).AssertSuccess();
		File.WriteAllText(jsfile, fsJsonPrinter.PrettyJson(data));
	}

	public static string BundleDir(string bundle, bool create = false)
	{
		var tfolder = dumpdir + Path.ChangeExtension(bundle, null);
		if (create)
			try
			{
				Directory.CreateDirectory(tfolder);
			}
			catch { };
		return tfolder;
	}

	public static void DumpCSV(string bundle, string asset, string v)
	{
		File.WriteAllText(BundleDir(bundle, true) + "/" + asset + ".csv", v);
	}

	public static IEnumerable<IEnumerable<string>> FetchCSV(string bundle, string asset)
	{
		return null;
	}

	static string dumpdir => UserData.Path + "/csv/";
	public static bool LoadLst(string bundle, string asset, out string[,] data)
	{
		var tfolder = BundleDir(bundle);
		var lstfile = tfolder + "/" + asset + ".lst";
		string text = null;
		data = null;


		if (Program.settings.fetchAssets)
		{
			try
			{
				text = System.Text.Encoding.UTF8.GetString(File.ReadAllBytes(lstfile));
			}
			catch { Trace.Back(); };
		}
		if (text == null)
		{
			var ta = CommonLib.LoadAsset<TextAsset>(bundle, asset);
			if (ta == null)
				return false;
			
			text = ta.text;
			if (Program.settings.dumpAssets)
			{
				try
				{
					try
					{
						Directory.CreateDirectory(tfolder);
					}
					catch { };

					File.WriteAllBytes(lstfile, System.Text.Encoding.UTF8.GetBytes(text));
				}
				catch { Trace.Back();  };
			}
		}


		YS_Assist.GetListString(text, out data);
		return true;
	}

	public static bool Asset(string bundle, string asset, System.Type type, string manifest, out AssetBundleLoadAssetOperation res)
	{
		res = null;
		if (Application.dataPath == null)
			return false;
		if (Program.settings == null)
			return false;
		if (asset == null)
			return false;

		var basedir = AssetBundleManager.BaseDownloadingURL;
		var tfolder = dumpdir + Path.ChangeExtension(bundle, null);

		if (!typeof(IDumpable).IsAssignableFrom(type))
			return false;
		if (!Program.settings.dumpAssets && !Program.settings.fetchAssets)
			return false;

		var csvfile = tfolder + "/" + asset + ".csv";

		if (Program.settings.fetchAssets)
		{
			try
			{
				var ex = ScriptableObject.CreateInstance(type) as IDumpable;
				var str = System.Text.Encoding.UTF8.GetString(File.ReadAllBytes(csvfile));
				if (str[0] == '\uFEFF')
					str = str.Substring(1);
				if (!ex.Unmarshal(str))
					return false;
				res = new AssetBundleLoadAssetOperationSimulation((Object)ex);
			}
			catch (FileNotFoundException) { }
			catch (IsolatedStorageException) { }
			catch (System.Exception ex)
			{
				return false;
			};
		}

		if (res != null)
			return true;

		try
		{
			if (Program.settings.dumpAssets)
				Directory.CreateDirectory(tfolder);
		}
		catch { };

		res = AssetBundleManager._LoadAsset(bundle, asset, type, manifest);

		if (!res.IsEmpty() && Program.settings.dumpAssets)
		{
			var ex = res.GetAsset<Object>() as IDumpable;
			if (ex == null)
			{
				try
				{
					Debug.Log($"[CACHE] Mismatched type {type.Name} -> {res.GetAsset<Object>()}");
				}
				catch { };
				return true;
			}

			if (Program.settings.dumpAssets)
			{
				Debug.Log($"[CACHE] Marshalling {csvfile}");
				var buf = ex.Marshal();
				if (buf != null)
				{
					if (Program.settings.useBOM)
						buf = "\uFEFF" + buf;
					File.WriteAllBytes(csvfile, System.Text.Encoding.UTF8.GetBytes(buf));
				}
			}
		}

		return true;
	}

	public static HashSet<string> ncache = new HashSet<string>();
	public static Texture2D LoadGPU(string bundle, string asset, string manifest)
	{
		if (manifest.IsNullOrEmpty())
			manifest = "abdata";
		var path = manifest + "/" + bundle + "/" + asset;
		if (ncache.Contains(path))
			return null;
		Texture2D tex = null;
		try
		{
			tex = AssetBundleManager.LoadAsset(bundle, asset, typeof(Texture2D), manifest).GetAsset<Texture2D>();
		} catch (System.Exception ex)
		{
			Debug.Log("Texture load failed with: " + ex.ToString());
		}
		if (tex == null)
		{
			ncache.Add(path);
		}
		Debug.Log($"[TEXTURE] {manifest}/{path} {tex.GetInstanceID()}");
		return tex;
	}
}

