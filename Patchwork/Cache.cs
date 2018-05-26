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


namespace Patchwork
{
	public class Cache
	{
		static string dumpdir => UserData.Path + "/csv/";

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
				return true;
			var ta = CommonLib.LoadAsset<TextAsset>(bundle, asset);
			if (ta == null)
				return false;
			var text = ta.text;
			if (text == null)
				return false;
			YS_Assist.GetListString(text, out data);
			if (Program.settings.dumpAssets && !File.Exists(ABPath(bundle, asset, "csv")))
			{
				var ex = ScriptableObject.CreateInstance<ExcelData>();
				ex.Import(CSV.ParseTSV(text));
				CSV.Save(ex, bundle, asset);
			}
			return true;
		}

		// Load plain string from cache
		public static string LoadString(string bundle, string asset, string suffix = "csv")
		{
			try
			{
				var str = System.Text.Encoding.UTF8.GetString(File.ReadAllBytes(ABPath(bundle, asset, suffix)));
				if (str[0] == '\uFEFF')
					str = str.Substring(1); // skip BOM
				return str;
			} catch
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
			} catch
			{
				return false;
			}
		}

		// Load generic asset. Serializable assets are re-routed to/from CSV.
		// Returns true if we *handle* the request, regardless of success.
		public static bool Asset(string bundle, string asset, System.Type type, string manifest, out AssetBundleLoadAssetOperation res)
		{
			res = null;
			if (Application.dataPath == null)
				return false;
			if (Program.settings == null)
				return false;
			if (asset == null)
				return false;

			if (!typeof(IDumpable).IsAssignableFrom(type))
				return false;
			if (Program.settings.fetchAssets)
			{
				var obj = CSV.Load(bundle, asset, type);
				if (obj != null)
				{
					res = new AssetBundleLoadAssetOperationSimulation(obj);
					return true;
				}
			}
			if (!Program.settings.dumpAssets)
				return false;

			res = AssetBundleManager._LoadAsset(bundle, asset, type, manifest);

			if (!res.IsEmpty())
				CSV.Save(res.GetAsset<UnityEngine.Object>(), bundle, asset);
			return true;
		}
	}
}