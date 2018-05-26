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
			if (Program.settings.dumpAssets)
			{
				var ex = CSV.ParseTSV(text);
				if (ex != null)
					CSV.Save(ex, bundle, asset);
			}
			return true;
		}

		// Load plain string from cache
		public static string LoadString(string bundle, string asset, string suffix = "csv")
		{
			var str = System.Text.Encoding.UTF8.GetString(File.ReadAllBytes(ABPath(bundle, asset, suffix)));
			if (str[0] == '\uFEFF')
				str = str.Substring(1); // skip BOM
			return str;
		}

		// Save plain string to cache
		public static bool SaveString(string buf, string bundle, string asset, string suffix = "csv")
		{
			var file = ABPath(bundle, asset, suffix);
			if (Program.settings.useBOM)
				buf = "\uFEFF" + buf;
			File.WriteAllBytes(file, System.Text.Encoding.UTF8.GetBytes(buf));
			return true; // XXX
		}

		// Load generic asset. Text assets may get special hacky treatment.
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
			if (!Program.settings.dumpAssets && !Program.settings.fetchAssets)
				return false;
			var obj = CSV.Load(bundle, asset, type);

			// Hmm, nothing found
			if (obj == null || !Program.settings.dumpAssets)
				return false;

			res = AssetBundleManager._LoadAsset(bundle, asset, type, manifest);
			// We know for certain it isn't anywhere now
			if (res.IsEmpty())
				return true;

			var ex = res.GetAsset<Object>() as IDumpable;
			// not actually dumpable
			if (ex == null)
				return true;
			CSV.Save(ex.Marshal(), bundle, asset);
			return true;
		}
	}
}