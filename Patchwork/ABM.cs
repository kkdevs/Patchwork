
using Patchwork;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;


/// <summary>
/// This is a down-to-earth replacement for the original trainwreck of asset bundle handling.
/// * Allows for asset name "conflicts" - meaning asset bundle path is now part of asset's locator
/// * Object instancing/caching instead of reloading those over and over
/// * Assets are garbage collected, not refcounted
/// </summary>
public class LoadedAssetBundle
{
	public static bool caching
	{
		get
		{
			return Program.settings.assetCache;
		}
	}
	public static string basePath;
	public AssetBundle m_AssetBundle;
	public string[] _assetNames;
	public bool isWild;
	public string path;
	public Dictionary<string, UnityEngine.Object> cache = new Dictionary<string, UnityEngine.Object>();
	public UnityEngine.Object[] allCache;
	public static Dictionary<string, LoadedAssetBundle> loadedBundles = new Dictionary<string, LoadedAssetBundle>();

	public string[] assetNames
	{
		get
		{
			if (_assetNames == null)
			{
				var t = m_AssetBundle.GetAllAssetNames();
				_assetNames = new string[t.Length];
				for (var i = 0; i < t.Length; i++)
					_assetNames[i] = Path.GetFileNameWithoutExtension(t[i]).ToLower();
			}
			return _assetNames;
		}
	}

	public static void FlushAllCaches()
	{
		foreach (var pair in loadedBundles)
		{
			foreach (var obj in pair.Value.cache)
			{
				Resources.UnloadAsset(obj.Value);
				UnityEngine.Object.DestroyImmediate(obj.Value);
			}
			if (pair.Value.m_AssetBundle!=null)
				pair.Value.m_AssetBundle.Unload(false);
		}
		loadedBundles.Clear();
		System.GC.Collect();
		System.GC.Collect();
	}

	public LoadedAssetBundle(AssetBundle assetBundle)
	{
		m_AssetBundle = assetBundle;
	}

	/// <summary>
	/// Get bundle reference if it is currently loaded.
	/// </summary>
	/// <param name="name"></param>
	/// <returns></returns>
	public static LoadedAssetBundle Get(string name)
	{
		LoadedAssetBundle res = null;
		loadedBundles.TryGetValue(name, out res);
		return res;
	}

	/// <summary>
	/// Attempt to load a bundle, returns either cached or new instance.
	/// </summary>
	/// <param name="name"></param>
	/// <param name="forasset"></param>
	/// <returns></returns>
	public static LoadedAssetBundle Load(string name, string forasset = null)
	{
		LoadedAssetBundle res = Get(name);
		if (res != null)
			return res;
		var path = basePath + name;
		if (!File.Exists(path))
		{
			Trace.Error($"[ABM] Path does not exist: {path}");
			return null;
		}
		Debug.Log($"[ABM] Registering new AB {name}");
		res = new LoadedAssetBundle(AssetBundle.LoadFromFile(path));
		res.path = path;
		res.isWild = (forasset == "*") || (forasset == null);
		loadedBundles[name] = res;
		return res;
	}

	static HashSet<Type> cacheables = new HashSet<Type>()
	{
		typeof(FaceBlendShape),
		typeof(Animator),
		typeof(DynamicBone),
		typeof(ChaAccessoryComponent),
		typeof(ChaClothesComponent),
		typeof(ChaCustomHairComponent),
		typeof(EyeLookController),
		typeof(Rigidbody),
	};

	public static bool IsCacheable(UnityEngine.Object obj)
	{
		if (!caching)
			return false;
		if (obj is Transform)
			return true;
		if (obj is Texture2D)
			return true;
		if (obj is Mesh)
			return true;
		if (obj is Material)
			return true;
		if (obj is ScriptableObject)
			return true;
		var go = obj as GameObject;
		if (go != null)
		{
			foreach (var comp in go.GetComponents<Component>())
				if (cacheables.Contains(comp.GetType()))
					return true;
		}
		return false;
	}

	public static UnityEngine.Object Clone(UnityEngine.Object obj)
	{
		if (obj is ScriptableObject)
			return obj;
		if (obj is Texture2D)
			return obj;
		if (obj is Material)
			return obj;
		if (obj is GameObject)
			return obj;
		return UnityEngine.Object.Instantiate(obj);
	}

	public IEnumerator LoadAssetAsync(string name, Type t, Action<UnityEngine.Object> cb)
	{
		Debug.Log($"[ABM] Async loading {name}");
		if (!caching || !cache.TryGetValue(name, out UnityEngine.Object obj))
		{
			if (!Ensure(name))
			{
				cb(null);
				yield break;
			}
			var req = m_AssetBundle.LoadAssetAsync(name, t);
			if (req == null)
			{
				Debug.Log($"[ABM] Async load {name} failed");
				cb(null);
				yield break;
			}
			yield return req;
			if (req.asset != null)
				cb(MaybeCache(name, req.asset));
			yield break;
		}
		cb(obj);
	}

	/// <summary>
	/// Load asset of a given type from this bundle.
	/// </summary>
	/// <param name="name"></param>
	/// <param name="typ"></param>
	/// <returns></returns>
	public UnityEngine.Object LoadAsset(string name, Type typ)
	{
		Debug.Log($"[ABM] Loading {path}/{name}");
		if (!caching || !cache.TryGetValue(name, out UnityEngine.Object obj))
		{
			if (!Ensure(name))
				return null;
			obj = m_AssetBundle.LoadAsset(name, typ);
			/*
			keeping the textures gpu only would be nice..
			var tex = obj as Texture2D;
			if (tex != null && tex.width >= 512)
			{
				tex.Apply(updateMipmaps: false, makeNoLongerReadable: true);
			}*/	
			if (obj == null)
				return null;
			return MaybeCache(name, obj);
		}
		return Clone(obj);
	}

	public UnityEngine.Object MaybeCache(string name, UnityEngine.Object obj)
	{
		if (obj == null)
			return null;
		if (IsCacheable(obj))
		{
			UnityEngine.Object.DontDestroyOnLoad(obj);
			Debug.Log($"[ABG] Caching {path}/{name}");
			cache[name] = obj;
		}
		else
		{
			if (obj is GameObject)
			{
				try
				{
					var comps = (obj as GameObject).GetComponents<Component>();
					foreach (var n in comps)
					{
						Debug.Log($"[ABM] Uncached GO Component: {n.GetType().Name}");
					}
				}
				catch { };
			}
		}
		return obj;
	}

	/// <summary>
	/// Load all assets of a given type from the bundle.
	/// </summary>
	/// <param name="typ"></param>
	/// <returns></returns>
	public UnityEngine.Object[] LoadAllAssets(Type typ)
	{
		Debug.Log($"[ABM] Loading {path}/*");
		if (!caching || allCache == null) {
			if (!Ensure())
				return null;
			if (caching)
			{
				allCache = m_AssetBundle.LoadAllAssets(typ);
				if (allCache == null)
					return null;
				for (var i = 0; i < allCache.Length; i++)
					UnityEngine.Object.DontDestroyOnLoad(allCache[i]);
			} 
			else
				return m_AssetBundle.LoadAllAssets(typ);
		}
		var ret = new UnityEngine.Object[allCache.Length];
		for (var i = 0; i < allCache.Length; i++)
			ret[i] = allCache[i];
		return ret;
	}

	/// <summary>
	/// Ensures that the bundle is actually loaded, kicking out any conflicting bundles in the process if needed.
	/// </summary>
	/// <param name="forname"></param>
	/// <returns>Whether the bundle is now loaded or not.</returns>
	public bool Ensure(string forname = null)
	{
		if (m_AssetBundle != null)
			return true;
		Debug.Log($"[ABM] There seems to be conflict for {path}/{forname}, trying to GC suspect bundles.");
		var low_forname = forname == null ? null : Path.GetFileNameWithoutExtension(forname.ToLower());
		foreach (var key in loadedBundles.Keys)
		{
			var lab = loadedBundles[key];
			if (lab.m_AssetBundle == null) // harmless
				continue;
			if ((lab.isWild && forname == null) || ((forname != null) && lab.assetNames.Contains(low_forname)))
			{
				Debug.Log($"[ABM] Unloading {lab.path}");
				lab.m_AssetBundle.Unload(false);
				lab.m_AssetBundle = null;
			}
		}
		m_AssetBundle = AssetBundle.LoadFromFile(path);
		if (m_AssetBundle == null)
		{
			Debug.Log($"[ABM] The load for {forname} failed anyway. Currently loaded bundles:");
			foreach (var item in loadedBundles)
			{
				if (item.Value.m_AssetBundle == null) continue;
				Debug.Log($"{item.Key}, assets={String.Join(",", item.Value.assetNames)}");
			}
			return false;
		}
		return true;
	}
}

