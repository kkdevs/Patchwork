using Patchwork;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
#if !USE_OLD_ABM

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
	public static int version;
	public static int level;
	public int locked;
	public static string basePath;
	public static string basePathCanon;
	public AssetBundle m_AssetBundle;
	private string[] assetNames;
	public string name;
	public string path;
	public Dictionary<string, UnityEngine.Object> cache = new Dictionary<string, UnityEngine.Object>();
	public UnityEngine.Object[] allCache;
	public static Dictionary<string, LoadedAssetBundle> loadedBundles = new Dictionary<string, LoadedAssetBundle>();

	public LoadedAssetBundle(string name)
	{
		this.name = name;
	}

	public string[] GetAllAssetNames()
	{
		if (assetNames == null) {
			if (!Ensure())
				return null;
			assetNames = m_AssetBundle.GetAllAssetNames();
		}
		return assetNames;
	}

	public static void FlushAllCaches()
	{
		ChaCustom.CustomSelectListCtrl.cache.cache.Clear();
		ChaCustom.CustomPushListCtrl.cache.cache.Clear();
		System.GC.Collect();
		System.GC.Collect();
		GCBundles();
		foreach (var ab in loadedBundles)
			ab.Value.cache.Clear();
	}

	public static void GCBundles()
	{
		foreach (var b in loadedBundles.Values)
			if (b.locked < version)
				b.Unload();
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
	/// Attempt to load a bundle and its dependenciess.
	/// </summary>
	/// <param name="name"></param>
	/// <param name="forasset"></param>
	/// <returns></returns>
	public static event Action<string, string> beforeBundleLoad;
	public static LoadedAssetBundle Load(string name, string forasset = null)
	{
		Debug.Log($"[ABM] Loading {name}");
		if (level == 0)
			beforeBundleLoad?.Invoke(name, forasset);
		var ab = Get(name);
		if (ab == null)
			ab = new LoadedAssetBundle(name);
		ab.path = Cache.GetPath(basePath + name);
		Debug.Log($"[ABM] Path {ab.path}");
		if (ab.path == null)
			return null;
		if (!File.Exists(ab.path))
			return null;
		loadedBundles[name] = ab;
		//if (ab.Ensure())
		//	return ab;
		return ab;
	}

	/// <summary>
	/// Ensures that the bundle is actually loaded, kicking out any conflicting bundles in the process if needed.
	/// </summary>
	/// <param name="forname"></param>
	/// <returns>Whether the bundle is now loaded or not.</returns>
	public bool Ensure(string forname = null)
	{
		level++;
		locked = version;
		if (deps.TryGetValue(name, out List<string> deplist))
			foreach (var dep in deplist)
				Load(dep)?.Ensure();
		if (m_AssetBundle == null)
		{
			m_AssetBundle = AssetBundle.LoadFromFile(path);
			if (m_AssetBundle == null)
			{
				GCBundles();
				m_AssetBundle = AssetBundle.LoadFromFile(path);
			}
		}
		level--;
		if (level == 0)
			version++;
		if (m_AssetBundle == null)
			Debug.Log($"[ABM] Failed to load {path} for {forname}");
		return m_AssetBundle != null;
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
		if (obj == null)
			return null;
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

	public static event Action<LoadedAssetBundle, string> beforeLoad;
	public IEnumerator LoadAssetAsync(string name, Type t, Action<UnityEngine.Object> cb)
	{
		beforeLoad?.Invoke(this, name);
		if (!caching || !cache.TryGetValue(name, out UnityEngine.Object obj))
		{
			Debug.Log($"[ABM] Cache miss {path}/{name}");
			if (!Ensure(name))
			{
				if (caching)
					cache[name] = null; // nxcache
				cb(null);
				yield break;
			}
			var req = m_AssetBundle.LoadAssetAsync(name, t);
			if (req == null)
			{
				Trace.Error($"[ABM] Async load {path}/{name} failed");
				Trace.Back("from");
				cb(null);
				yield break;
			}
			yield return req;
			if (req.asset != null)
				cb(MaybeCache(name, req.asset));
			yield break;
		}
		Debug.Log($"[ABM] Cache hit {path}/{name}");
		cb(obj);
	}

	/// <summary>
	/// Load asset of a given type from this bundle.
	/// </summary>
	/// <param name="name"></param>
	/// <param name="typ"></param>
	/// <returns></returns>
	public UnityEngine.Object LoadAsset(string name, Type typ, bool nocache=false)
	{
		beforeLoad?.Invoke(this, name);
		//Debug.Log($"[ABM] Loading {path}/{name}");
		if (nocache || !caching || !cache.TryGetValue(name, out UnityEngine.Object obj))
		{
			Debug.Log($"[ABM] Cache miss {path}/{name}");
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
			{
				if (caching)
					cache[name] = null; // nxcache
				Trace.Error($"[ABM] Load {path}/{name} failed");
				Trace.Back("from");
				return null;
			}
			if (nocache)
				return obj;
			return MaybeCache(name, obj);
		}
		Debug.Log($"[ABM] Cache hit {path}/{name}.");
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


	public static Dictionary<string, List<string>> deps = new Dictionary<string, List<string>>();
	public static void Init(string bp)
	{
		basePath = bp;
		basePathCanon = Path.GetFullPath(bp).ToLower();
		if (!Directory.Exists(basePath)) return;
		if (!Directory.Exists(basePathCanon)) return;
		Trace.Log($"[ABM] Init {bp} {basePathCanon}");
		foreach (var man in Directory.GetFiles(basePath, "*.*", SearchOption.TopDirectoryOnly))
		{
			var fn = Path.GetFileNameWithoutExtension(man);
			var ab = AssetBundle.LoadFromFile(man);
			Debug.Log($"[ABM] Manifest load {man}");
			if (ab == null) continue;
			var abm = ab.LoadAsset<AssetBundleManifest>("AssetBundleManifest");
			if (abm == null) continue;
			ab.Unload(false);
			AssetBundleManager.ManifestBundlePack[fn] = new AssetBundleManager.BundlePack() { AssetBundleManifest = abm };
			foreach (var sab in abm.GetAllAssetBundles())
			{
				if (!deps.TryGetValue(sab, out List<string> bdeps))
					deps[sab] = new List<string>();
				foreach (var dep in abm.GetAllDependencies(sab))
					if (!deps[sab].Contains(dep))
						deps[sab].Add(dep);
			}
		}
	}
	public void Unload(bool wipe = false)
	{
		if (m_AssetBundle != null)
		{
			m_AssetBundle.Unload(false);
			Debug.Log("[ABM] Unloading " + name);
		}
		m_AssetBundle = null;
	}
}


#endif

