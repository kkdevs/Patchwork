// This is a minimal replacement for the original game's ABM
// * Allows for asset name "conflicts" - meaning asset bundle path is now part of asset's locator
// * Object instancing/caching instead of reloading those over and over
// * Assets are garbage collected, not refcounted

using Patchwork;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

public class AssetBundleManifestData : AssetBundleData
{
	public string manifest { get; set; }
	public AssetBundleManifestData(string bundle, string asset, string manifest)
	: base(bundle, asset)
	{
		manifest = manifest;
	}
	public AssetBundleManifestData()
	{
	}
}

public class LoadedAssetBundle
{
	public AssetBundle m_AssetBundle;
	public string[] _assetNames;
	public bool isWild;
	public string path;
	public Dictionary<string, UnityEngine.Object> cache = new Dictionary<string, UnityEngine.Object>();
	public string[] assetNames
	{
		get
		{
			if (_assetNames == null)
				_assetNames = m_AssetBundle.GetAllAssetNames();
			return _assetNames;
		}
	}

	public LoadedAssetBundle(AssetBundle assetBundle)
	{
		m_AssetBundle = assetBundle;
	}
	public LoadedAssetBundle(AssetBundle assetBundle, bool wild, string path)
	{
		isWild = wild;
		m_AssetBundle = assetBundle;
	}

}

public partial class AssetBundleManager : Singleton<AssetBundleManager>
{
	public static bool isInitialized = false;
	public static string BaseDownloadingURL { get; set; }
	public static void Initialize(string basePath)
	{
		if (!isInitialized)
		{
			BaseDownloadingURL = basePath;
			GameObject gameObject = new GameObject("AssetBundleManager", typeof(AssetBundleManager));
			UnityEngine.Object.DontDestroyOnLoad(gameObject);
			isInitialized = true;
			InitAddComponent.AddComponents(gameObject);
		}
	}

	static Dictionary<string, LoadedAssetBundle> loadedBundles = new Dictionary<string, LoadedAssetBundle>();
	public static LoadedAssetBundle GetLoadedAssetBundle(string assetBundleName, out string error, string manifestAssetBundleName = null)
	{
		error = null;
		if (loadedBundles.TryGetValue(assetBundleName, out LoadedAssetBundle ret))
			return ret;
		error = assetBundleName + " failed to load.";
		return null;
	}

	public static void UnloadAssetBundle(string assetBundleName, bool isUnloadForceRefCount, string manifestAssetBundleName = null, bool unloadAllLoadedObjects = false)
	{
		// nop
	}

	public static LoadedAssetBundle LoadAssetBundle(string assetBundleName, bool isAsync, string manifestAssetBundleName, string forasset)
	{
		bool wild = forasset == "*";
		string err;
		var res = GetLoadedAssetBundle(assetBundleName, out err);
		if (res != null)
		{
			Debug.Log($"[ABM] {assetBundleName} found in cache, so using that.");
			return res;
		}
		Debug.Log($"[ABM] {assetBundleName} not found in cache, loading...");
		string path = BaseDownloadingURL + assetBundleName;
		var ab = AssetBundle.LoadFromFile(path);
		if (ab == null)
		{
			Debug.Log($"[ABM] There seems to be conflict for {forasset}, trying to find the suspect.");
			// The path indeed doesn't exist, so don't bother.
			if (!File.Exists(path))
			{
				Debug.Log($"[ABM] {path} not found, bail.");
				return null;
			}
			// Looks like we have a name conflict. Comb through the cache and look for names.
			foreach (var key in loadedBundles.Keys) {
				var lab = loadedBundles[key];
				if ((lab.isWild && wild) || lab.assetNames.Contains(forasset))
				{
					lab.m_AssetBundle.Unload(false);
					lab.m_AssetBundle = null;
					Debug.Log($"[ABM] Kicking out {key} because it conflicts with {assetBundleName}, for asset {forasset}");
				}
			}
			ab = AssetBundle.LoadFromFile(path);
			if (ab == null)
			{
				Debug.Log($"[ABM] The load failed anyway, giving up...");
				return null;
			}
		}
		return (loadedBundles[assetBundleName] = new LoadedAssetBundle(ab, wild, path));
	}

	public static AssetBundleLoadAssetOperation LoadAsset(string assetBundleName, string assetName, Type type, string manifestAssetBundleName = null)
	{
		if (Cache.Asset(assetBundleName, assetName, type, manifestAssetBundleName, out AssetBundleLoadAssetOperation cached))
			return cached;
		return _LoadAsset(assetBundleName, assetName, type, manifestAssetBundleName);
	}
	public static AssetBundleLoadAssetOperation _LoadAsset(string assetBundleName, string assetName, Type type, string manifestAssetBundleName = null)
	{
		var b = LoadAssetBundle(assetBundleName, false, null, assetName);
		if (b == null)
			return null;
		return new AssetBundleLoadAssetOperationSimulation(b.m_AssetBundle.LoadAsset(assetName, type));
	}

	public static AssetBundleLoadAssetOperation LoadAllAsset(string assetBundleName, Type type, string manifestAssetBundleName = null)
	{
		var b = LoadAssetBundle(assetBundleName, false, null, "*");
		if (b == null)
			return null; // maybe return Object[] ?
		return new AssetBundleLoadAssetOperationSimulation(b.m_AssetBundle.LoadAllAssets(type));
	}

	public static AssetBundleLoadOperation LoadLevel(string assetBundleName, string levelName, bool isAdditive, string manifestAssetBundleName = null)
	{
		var b = LoadAssetBundle(assetBundleName, false, null, levelName);
		SceneManager.LoadScene(levelName, isAdditive ? LoadSceneMode.Additive : LoadSceneMode.Single);
		return new AssetBundleLoadLevelSimulationOperation();
	}

	public static AssetBundleLoadOperation LoadLevelAsync(string assetBundleName, string levelName, bool isAdditive, string manifestAssetBundleName = null)
	{
		LoadAssetBundle(assetBundleName, false, null, levelName);
		return new AssetBundleLoadLevelOperation(assetBundleName, levelName, isAdditive, manifestAssetBundleName);
	}

	public static float Progress
	{
		get
		{
			return 1;
		}
	}

	public static void GC()
	{
	}
}



