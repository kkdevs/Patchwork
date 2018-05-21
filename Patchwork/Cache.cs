using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Patchwork;
using UnityEngine;
using System.IO.IsolatedStorage;

public class Cache
{
	static string dumpdir => Application.dataPath + "/../mod/";
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

		if (type != typeof(ExcelData))
			return false;
		if (!Program.settings.dumpAssets && !Program.settings.fetchAssets)
			return false;

		var csvfile = tfolder + "/" + asset + ".csv";

		if (Program.settings.fetchAssets)
		{
			try
			{
				var ex = ScriptableObject.CreateInstance(typeof(ExcelData)) as ExcelData;
				ex.Decode(System.Text.Encoding.UTF8.GetString(File.ReadAllBytes(csvfile)));
				res = new AssetBundleLoadAssetOperationSimulation(ex);
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
			var ex = res.GetAsset<ExcelData>();
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
				var buf = ex.GetCSV2();
				File.WriteAllBytes(csvfile, System.Text.Encoding.UTF8.GetBytes(buf));
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

