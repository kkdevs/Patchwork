// These are virtually all no-op wrappers.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;


public partial class AssetBundleManager : Singleton<AssetBundleManager>
{
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

