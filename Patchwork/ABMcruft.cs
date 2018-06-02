// These are all no-op wrappers to make rest of the code happy.

using Patchwork;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class AssetBundleCheck
{
	public static bool IsSimulation => false;

	public static bool IsFile(string assetBundleName, string fileName = "")
	{
		if (!File.Exists(AssetBundleManager.BaseDownloadingURL + assetBundleName))
			return false;
		return true;
	}

	public static string[] GetAllAssetName(string assetBundleName, bool _WithExtension = true, string manifestAssetBundleName = null, bool isAllCheck = false)
	{
		var ab = LoadedAssetBundle.Load(assetBundleName);
		if ((ab == null) || (!ab.Ensure())) return null;
		var assetBundle = ab.m_AssetBundle;
		return (!_WithExtension) ? assetBundle.GetAllAssetNames().Select(Path.GetFileNameWithoutExtension).ToArray() : assetBundle.GetAllAssetNames().Select(Path.GetFileName).ToArray();
	}
}

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

public class AssetBundleManager : Singleton<AssetBundleManager>
{
	public static bool isInitialized = false;
	public static string BaseDownloadingURL { get; set; }
	public static void Initialize(string basePath)
	{
		if (!isInitialized)
		{
			BaseDownloadingURL = basePath;
			LoadedAssetBundle.basePath = basePath;
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
		return LoadedAssetBundle.Get(assetBundleName);
	}

	public static void UnloadAssetBundle(string assetBundleName, bool isUnloadForceRefCount, string manifestAssetBundleName = null, bool unloadAllLoadedObjects = false)
	{
	}

	public static LoadedAssetBundle LoadAssetBundle(string assetBundleName, bool isAsync, string manifestAssetBundleName, string forasset)
	{
		return LoadedAssetBundle.Load(assetBundleName, forasset);
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
		return new AssetBundleLoadAssetOperationSimulation(b.LoadAsset(assetName, type));
	}

	public static AssetBundleLoadAssetOperation LoadAssetAsync(AssetBundleData data, Type type)
	{
		return LoadAssetAsync(data.bundle, data.asset, type, null);
	}

	public static AssetBundleLoadAssetOperation LoadAssetAsync(AssetBundleManifestData data, Type type)
	{
		return LoadAssetAsync(data.bundle, data.asset, type, data.manifest);
	}

	public static AssetBundleLoadAssetOperation LoadAssetAsync(string assetBundleName, string assetName, Type type, string manifestAssetBundleName = null)
	{
		return new AssetBundleLoadAssetOperationFull(assetBundleName, assetName, type, manifestAssetBundleName);
	}


	public static AssetBundleLoadAssetOperation LoadAllAsset(string assetBundleName, Type type, string manifestAssetBundleName = null)
	{
		var b = LoadAssetBundle(assetBundleName, false, null, "*");
		if (b == null)
			return null; // maybe return Object[] ?
		return new AssetBundleLoadAssetOperationSimulation(b.LoadAllAssets(type));
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

	public static LoadedAssetBundle LoadAssetBundle(string assetBundleName, bool isAsync, string manifestAssetBundleName = null)
	{
		return LoadAssetBundle(assetBundleName, isAsync, manifestAssetBundleName, null);
	}
	public static AssetBundleLoadAssetOperation LoadAllAsset(AssetBundleData data, Type type)
	{
		return LoadAllAsset(data.bundle, type, null);
	}

	public static void UnloadAssetBundle(AssetBundleData data, bool isUnloadForceRefCount, bool unloadAllLoadedObjects = false)
	{
		UnloadAssetBundle(data.bundle, isUnloadForceRefCount, null, unloadAllLoadedObjects);
	}

	public static void UnloadAssetBundle(AssetBundleManifestData data, bool isUnloadForceRefCount, bool unloadAllLoadedObjects = false)
	{
		UnloadAssetBundle(data.bundle, isUnloadForceRefCount, data.manifest, unloadAllLoadedObjects);
	}
	public static AssetBundleLoadOperation LoadLevelAsync(AssetBundleData data, bool isAdditive)
	{
		return LoadLevelAsync(data.bundle, data.asset, isAdditive, null);
	}

	public static AssetBundleLoadOperation LoadLevelAsync(AssetBundleManifestData data, bool isAdditive)
	{
		return LoadLevelAsync(data.bundle, data.asset, isAdditive, data.manifest);
	}
	public static AssetBundleLoadOperation LoadLevel(AssetBundleData data, bool isAdditive)
	{
		return LoadLevel(data.bundle, data.asset, isAdditive, null);
	}

	public static AssetBundleLoadOperation LoadLevel(AssetBundleManifestData data, bool isAdditive)
	{
		return LoadLevel(data.bundle, data.asset, isAdditive, data.manifest);
	}
	public static AssetBundleLoadAssetOperation LoadAsset(AssetBundleData data, Type type)
	{
		return LoadAsset(data.bundle, data.asset, type, null);
	}

	public static AssetBundleLoadAssetOperation LoadAsset(AssetBundleManifestData data, Type type)
	{
		return LoadAsset(data.bundle, data.asset, type, data.manifest);
	}
}

public class AssetBundleData
{
	public string bundle = string.Empty;

	public string asset = string.Empty;
	public AssetBundleLoadAssetOperation request;

	public virtual LoadedAssetBundle LoadedBundle
	{
		get
		{
			string text;
			return AssetBundleManager.GetLoadedAssetBundle(bundle, out text, null);
		}
	}
	public bool isFile
	{
		get
		{
			if (LoadedBundle != null)
				return true;
			if (File.Exists(AssetBundleManager.BaseDownloadingURL + bundle))
				return true;
			return false;
		}
	}

	public bool isEmpty => bundle.IsNullOrEmpty() || asset.IsNullOrEmpty();

	public AssetBundleData()
	{
	}
	public AssetBundleData(string bundle, string asset)
	{
		this.bundle = bundle;
		this.asset = asset;
	}
	public bool Check(string bundle, string asset)
	{
		if (!asset.IsNullOrEmpty() && this.asset != asset)
			return true;
		if (!bundle.IsNullOrEmpty() && this.bundle != bundle)
			return true;
		return false;
	}
	public static List<string> GetAssetBundleNameListFromPath(string path, bool subdirCheck = false)
	{
		List<string> result = new List<string>();
		string basePath = AssetBundleManager.BaseDownloadingURL;
		string path2 = basePath + path;
		if (!Directory.Exists(path2))
			return result;
		string[] source = (!subdirCheck) ? Directory.GetFiles(path2, "*.unity3d") : Directory.GetFiles(path2, "*.unity3d", SearchOption.AllDirectories);
		return (from s in source
				select s.Replace(basePath, string.Empty)).ToList();
	}

	public virtual AssetBundleLoadAssetOperation LoadBundle<T>() where T : UnityEngine.Object
	{
		if (!isFile)
			return null;
		return request ?? (request = AssetBundleManager.LoadAsset(this, typeof(T)));
	}
	public virtual T GetAsset<T>() where T : UnityEngine.Object
	{
		if (request == null)
			request = LoadBundle<T>();
		if (request == null)
			return (T)null;
		return request.GetAsset<T>();
	}
	public IEnumerator GetAsset<T>(Action<T> act) where T : UnityEngine.Object
	{
		if (request == null)
			request = this.LoadBundle<T>();
		if (request != null)
		{
			yield return (object)request;
			if (!request.IsEmpty())
				NullCheck.Call<T>(act, request.GetAsset<T>());
		}
	}

	public virtual T[] GetAllAssets<T>() where T : UnityEngine.Object
	{
		if (request == null)
			request = LoadAllBundle<T>();
		if (request == null)
			return null;
		return request.GetAllAssets<T>();
	}
	public virtual AssetBundleLoadAssetOperation LoadAllBundle<T>() where T : UnityEngine.Object
	{
		if (!isFile)
			return null;
		return request ?? (request = AssetBundleManager.LoadAllAsset(this, typeof(T)));
	}
	public virtual void UnloadBundle(bool isUnloadForceRefCount = false, bool unloadAllLoadedObjects = false)
	{
		if (request != null)
			AssetBundleManager.UnloadAssetBundle(this, isUnloadForceRefCount, unloadAllLoadedObjects);
		request = null;
	}
}

