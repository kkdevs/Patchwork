using UnityEngine;
using Illusion.Extensions;
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
using System;

namespace Patchwork
{
	public class Cache
	{
		public static SaveFrameAssist saveFrameAssist;
		public static string dumpdir => UserData.Path + "/csv/";

		// Resolve cache folder from bundle name
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

		// Get path from AB
		public static string ABPath(string bundle, string asset, string suffix, bool create = false)
		{
			var bd = BundleDir(bundle, create);
			return bd + "/" + asset + "." + suffix;
		}

		// Load a lst. First try cache, then bundle, dump as csv if enabled.
		public static bool LoadLst(string bundle, string asset, out string[,] data)
		{
			data = null;
			if (CSV.LoadLst(bundle, asset, out data))
			{
				Debug.Log($"[CACHE] Loading LST {bundle}/{asset}");
				return true;
			}
			var ta = CommonLib.LoadAsset<TextAsset>(bundle, asset);
			if (ta == null)
				return false;
			var text = ta.text;
			if (text == null)
				return false;
			YS_Assist.GetListString(text, out data);
			if (Program.settings.dumpAssets && !File.Exists(ABPath(bundle, asset, "csv")))
			{
				Debug.Log($"[CACHE] Dumped LST {bundle}/{asset}");
				var ex = New<ExcelData>();
				ex.Import(CSV.ParseTSV(text));
				Save(ex, bundle, asset);
			}
			return true;
		}

		public static T New<T>() where T : class, new()
		{
			return New(typeof(T)) as T;
		}
		public static object New(Type t)
		{
			if (typeof(ScriptableObject).IsAssignableFrom(t))
			{
				return ScriptableObject.CreateInstance(t);
			}
			else
			{
				return Activator.CreateInstance(t);
			}
		}

		public static string LoadString(string bundle, string asset, string suffix = "csv")
		{
			return LoadString(ABPath(bundle, asset, suffix));
		}
		// Load plain string from cache
		public static string LoadString(string path)
		{
			try
			{
				var str = System.Text.Encoding.UTF8.GetString(File.ReadAllBytes(path));
				if (str[0] == '\uFEFF')
					str = str.Substring(1); // skip BOM
				return str;
			}
			catch
			{
				return null;
			}
		}

		// Save plain string to cache
		public static bool SaveString(string buf, string bundle, string asset, string suffix = "csv")
		{
			try
			{
				var file = ABPath(bundle, asset, suffix, true); // makes the folder too
				if (Program.settings.useBOM)
					buf = "\uFEFF" + buf;
				File.WriteAllBytes(file, System.Text.Encoding.UTF8.GetBytes(buf));
				return true; // XXX
			}
			catch
			{
				return false;
			}
		}

		public static T Load<T>(string bundle, string asset, string ext = "csv") where T : class, IDumpable, new()
		{
			return Load(bundle, asset, typeof(T), ext) as T;
		}
		public static T Load<T>(string path, string ext = "csv") where T : class, IDumpable, new()
		{
			return Load(path, typeof(T), ext) as T;
		}
		public static object Load(string bundle, string asset, Type typ, string ext = "csv")
		{
			return Load(ABPath(bundle, asset, ext), typ, ext);
		}
		// Load marhsalled type from csv
		public static object Load(string path, Type typ, string ext = null)
		{
			var str = Cache.LoadString(path);
			if (str == null)
				return null;
			var ex = New(typ) as IDumpable;
			if (ext == null)
				ext = ex.GetFileExt();
			if (!ex.Unmarshal(str, ext))
				return null;
			return ex;
		}

		// Save marshalled type to csv
		public static bool Save(object o, string bundle, string asset)
		{
			var ex = o as IDumpable;
			var ext = ex.GetFileExt();
			// already saved
			if (!Program.settings.dumpAssets || File.Exists(ABPath(bundle, asset, ext)))
				return true;
			if (ex == null)
				return false;
			var str = ex.Marshal();
			if (str == null)
				return false;
			return Cache.SaveString(str, bundle, asset, ext);
		}

		// Load generic asset. Serializable assets are re-routed to/from CSV.
		// Returns true if we *handle* the request, regardless of success.
		public static bool Asset(string bundle, string asset, System.Type type, string manifest, out AssetBundleLoadAssetOperation res)
		{
			res = null;
			if (asset == null)
				return false;

			if (!typeof(IDumpable).IsAssignableFrom(type))
				return false;
			if (Program.settings.fetchAssets)
			{
				var obj = Load(bundle, asset, type) as UnityEngine.Object;
				if (obj != null)
				{
					Debug.Log($"[CACHE] Loading CSV {bundle}/{asset}");
					res = new AssetBundleLoadAssetOperationSimulation(obj);
					return true;
				}
			}
			if (!Program.settings.dumpAssets)
				return false;

			res = AssetBundleManager._LoadAsset(bundle, asset, type, manifest);

			if (!res.IsEmpty() && !File.Exists(ABPath(bundle, asset, "csv")))
			{
				Debug.Log($"[CACHE] Saving CSV {bundle}/{asset}");
				Save(res.GetAsset<UnityEngine.Object>(), bundle, asset);
			}
			return true;
		}

		public static string Base(ChaInfo who, string typ = "oo")
		{
			if (typ == "oo")
			{
				return (who.sex == 0 ? Program.settings.ooMale : Program.settings.ooFemale) + ".unity3d";
			}
			else
			{
				return (who.sex == 0 ? Program.settings.mmMale : Program.settings.mmFemale) + ".unity3d";
			}
			return null;
		}


		public static List<string[,]> LoadMultiLst(string bundledir, string asset)
		{
			var res = new List<string[,]>();
			//asset = asset.ToLower();
			List<string> bundles = CommonLib.GetAssetBundleNameListFromPath(bundledir, false);
			foreach (var bn in bundles)
			{
				string[,] entry = null;
				if (Program.settings.fetchAssets)
					if (CSV.LoadLst(bn, asset, out entry)) {
						res.Add(entry);
						continue;
					}
				var ta = LoadedAssetBundle.Load(bn)?.LoadAsset(asset, typeof(TextAsset)) as TextAsset;
				if (ta == null)
					continue;
				var ex = ScriptableObject.CreateInstance<ExcelData>();
				ex.Import(CSV.ParseTSV(ta.text));
				Save(ex, bn, asset);
				CSV.LoadLst(ex, out entry);
				res.Add(entry);
			}
			Script.registry[asset] = res;
			return res;
		}
	}
}

public partial class GlobalMethod
{
	public static string[,] gArray;
	// XXX get rid of this horrible hack. Or at least add ability to concat files too.
	public static string LoadAllListText(string _assetbundleFolder, string _strLoadFile, List<string> _OmitFolderName = null)
	{
		if (_strLoadFile.IsNullOrEmpty())
			return null;
		if (_strLoadFile.StartsWith("dan_kh"))
		{
			Trace.Back("HDAN!");
		}
		if (Program.settings.fetchAssets)
		{
			if (CSV.LoadLst(_assetbundleFolder, _strLoadFile, out gArray))
			{
				Debug.Log($"[CACHE] Loading multi-LST {_assetbundleFolder}/{_strLoadFile}");
				return "@garray";
			}
		}
		var res = _LoadAllListText(_assetbundleFolder, _strLoadFile, _OmitFolderName);
		if (res == "" || res == null || !Patchwork.Program.settings.dumpAssets || File.Exists(Cache.ABPath(_assetbundleFolder, _strLoadFile, "csv")))
			return res;
		Debug.Log($"[CACHE] Saving multi-LST {_assetbundleFolder}/{_strLoadFile}");
		var ex = ScriptableObject.CreateInstance<ExcelData>();
		ex.Import(CSV.ParseTSV(res));
		Cache.Save(ex, _assetbundleFolder, _strLoadFile);
		return res;
	}
}
