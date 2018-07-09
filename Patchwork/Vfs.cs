using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using MessagePack;
using System.Collections;
using static Patchwork;

public static class Vfs
{
	// These folders contain bundles in increasing DLC order. Often the game asks
	// about list of bundles in there, so we need to make a virtualized list for things we're adding.
	// If folders are present in a mod, they masquerade as new asset bundles in the listing, too.
	public static List<string> listable = new List<string>()
	{
		"map/actionpoint",
		"map/waitpoint",
		"map/list/calcgateinfo",
		"map/list/navigationinfo",
		"action/list/waitpoint/non",
		"action/list/fixevent",
		"action/list/chara",
		"action/list/talklookneck",
		"action/list/talklookbody",
		"action/list/event",
		"action/list/clubinfo",
		"action/list/event",
		"etcetra/list/exp",
		"action/playeraction",
		"action/actioncontrol",
		"etcetra/list/nickname",
		"studio/info",
		"adv/scenario",
		"sound/data/systemse/titlecall",
		"action/fixchara",
		"sound/data/systemse/brandcall",
		"communication",

		// ! - no recursion
		"!h/list",
		"!action/list/sound/bgm",
		"!map/sound",
		"!list/characustom",
		"!action/list/sound/se/action",

	};

	[MessagePackObject]
	public class DirCache
	{
		[Key(0)]
		public Dictionary<string, LoadedAssetBundle> abs = new Dictionary<string, LoadedAssetBundle>();
		[Key(1)]
		public Dictionary<string, List<string>> dirLists = new Dictionary<string, List<string>>();
	}

	public static string BodyBase(ChaInfo who, string typ = "oo")
	{
		if (typ == "oo")
		{
			return (who.sex == 0 ? settings.ooMale : settings.ooFemale) + ".unity3d";
		}
		else
		{
			return (who.sex == 0 ? settings.mmMale : settings.mmFemale) + ".unity3d";
		}
		return null;
	}

	/// <summary>
	/// Load top level manifests to track dependencies
	/// </summary>
	public static void LoadManifests()
	{
		foreach (var man in Directory.GetFiles(Dir.abdata, "*.*", SearchOption.TopDirectoryOnly))
		{
			var ab = AssetBundle.LoadFromFile(man);
			var fn = man.Substring(Dir.abdata.Length);
			if (ab == null) continue;
			var mf = ab.LoadAsset<AssetBundleManifest>("AssetBundleManifest");
			if (mf != null)
			{
				AssetBundleManager.ManifestBundlePack[fn] = new AssetBundleManager.BundlePack() { AssetBundleManifest = mf };
				foreach (var sab in mf.GetAllAssetBundles())
				{
					var abi = LoadedAssetBundle.Make(sab);
					foreach (var dep in mf.GetAllDependencies(sab))
						if (!abi.deps.Contains(dep))
							abi.deps.Add(dep);
				}
			}
			ab.Unload(true);
		}
	}

	/// <summary>
	/// Rescan the directory mappings
	/// </summary>
	public static void Rescan()
	{
		string abdata = "abdata/";
		string inabdata = Dir.root + abdata;
		dc.abs.Clear();
		dc.dirLists.Clear();
		// initially map everything to self
		foreach (var bundle in Directory.GetFiles(inabdata, "*.unity3d", SearchOption.AllDirectories))
		{
			var rel = bundle.Replace("\\","/").Substring(Dir.root.Length);
			LoadedAssetBundle.Make(rel).realPath = rel;
		}

		Dictionary<string,bool> lhash = new Dictionary<string, bool>();
		// Now scan for listable bundles in abdata
		foreach (var ld in listable)
		{
			var lld = ld.Replace("!", "");
			lhash[lld] = lld != ld;
			if (!Directory.Exists(lld)) continue;
			foreach (var blist in Directory.GetFiles(Dir.abdata + lld, "*.unity3d", ld != lld ? SearchOption.TopDirectoryOnly : SearchOption.AllDirectories))
				dc.dirLists[lld].Add(blist.Replace("\\","/").Substring(Dir.root.Length));
		}

		// now scan mods, those will simply override the tables above
		foreach (var mod in Directory.GetDirectories(Dir.mod, "*.*", SearchOption.TopDirectoryOnly))
		{
			var inmod = mod + "/";
			foreach (var bundle in Directory.GetFiles(inmod, "*.*", SearchOption.AllDirectories))
			{
				var bd = bundle.Replace("\\", "/");
				var fn = bd.Substring(Dir.root.Length);

				// may override original abdata mapping
				LoadedAssetBundle.Make("abdata/" + bd.Substring(inmod.Length)).realPath = fn;

				// keep stripping last path component of the file path until we match
				// a path which is listable
				fn = bd.Substring(Dir.abdata.Length);
				var ofn = fn;
				int ind;
				int stripped = 0;
				while ((ind = fn.LastIndexOf('/')) >= 0)
				{
					fn.Remove(ind);
					// a file inside listable folder was detected, we'll need to inspect the folder
					if (lhash.TryGetValue(fn, out bool norecurse))
					{
						// an actual bundle, insert it as-is
						if (ofn.EndsWith(".unity3d"))
						{
							if ((!norecurse || stripped == 0) && !dc.dirLists[fn].Contains(ofn))
								dc.dirLists[fn].Add(ofn);
						} else
						{
							// a file in a directory. the directory itself then becomes a virtual bundle
							// if there is no accompanying real one
							var virtab = ofn.Remove(ofn.LastIndexOf('/')) + ".unity3d";

							// add to dir listings
							if ((!norecurse || stripped == 0) || !dc.dirLists[fn].Contains(virtab))
							{
								dc.dirLists[fn].Add(virtab);
							}
							// create a virtual bundle
							var assname = Path.GetFileName(fn);
							assname = assname.Remove(assname.LastIndexOf('.'));
							LoadedAssetBundle.Make(virtab).virtualAssets[assname] = fn;
						}
					}
					stripped++;
				}
			}
		}
	}

	public static DirCache dc = new DirCache();

	public static void Init()
	{
		Rescan();
		LoadManifests();
	}


	public static List<string> GetAssetBundleNameListFromPath(string path, bool recurse)
	{
		if (path.EndsWith("/"))
			path = path.Remove(path.Length - 1);
#if GAME_DEBUG
		if (recurse && listable.Contains("!" + path))
		{
			Debug.Error("Recurse/norecurse mismatch for ", path);
		}
#endif
		return dc.dirLists[path];
	}

	public static bool GetSprite(string bundle, string name, out Sprite ret)
	{
		ret = null;
		var tex = LoadedAssetBundle.Load(bundle)?.LoadAsset(name, typeof(Texture2D)) as Texture2D;
		if (tex == null)
			return false;
		ret = Sprite.Create(tex, new Rect(0f, 0f, (float)tex.width, (float)tex.height), new Vector2(0.5f, 0.5f));
		return true;
	}
}

[MessagePackObject]
public class LoadedAssetBundle
{
	[Key(0)]
	public string name;
	[Key(1)]
	public string realPath;
	[Key(2)]
	public List<string> deps = new List<string>();
	[Key(3)]
	public string[] assetNames;
	[Key(4)]
	public Dictionary<string, string> virtualAssets = new Dictionary<string, string>();

	[NonSerialized]
	public AssetBundle m_AssetBundle;

	public bool isVirtual => realPath != null && !realPath.EndsWith(".unity3d") && virtualAssets.Count > 0;

	public static Dictionary<string, LoadedAssetBundle> cache => Vfs.dc.abs;
	public LoadedAssetBundle(string name)
	{
		this.name = name;
	}

	public static bool Flush(bool t)
	{
		return true;
	}
	public static bool FlushAllCaches()
	{
		return true;
	}


	/// <summary>
	/// Get or create new empty ab
	/// </summary>
	/// <param name="name"></param>
	/// <returns></returns>
	public static LoadedAssetBundle Make(string name)
	{
		if (cache.TryGetValue(name, out LoadedAssetBundle ab))
			return ab;
		return cache[name] = new LoadedAssetBundle(name);
	}

	/// <summary>
	/// Get existing ab
	/// </summary>
	/// <param name="name"></param>
	/// <returns></returns>
	public static LoadedAssetBundle Get(string name)
	{
		if (cache.TryGetValue(name, out LoadedAssetBundle ab))
			return ab;
		return null;
	}

	/// <summary>
	/// Load the. Same as get, but ensures that assets can be now loaded.
	/// </summary>
	/// <param name="name"></param>
	/// <returns></returns>
	public static LoadedAssetBundle Load(string name, string forasset = null)
	{
		var ab = Get(name);
		if (ab != null)
			return ab;

		if (ab.realPath == null)
			return null;
		return ab;
	}

	public UnityEngine.Object[] LoadAllAssets(Type typ)
	{
		if (!Ensure())
			return null;
		List<UnityEngine.Object> allAss = new List<UnityEngine.Object>();
		if (m_AssetBundle != null)
			allAss.AddRange(m_AssetBundle.LoadAllAssets(typ).ToList());
		Dictionary<string, UnityEngine.Object> names = new Dictionary<string, UnityEngine.Object>();
		foreach (var ass in allAss)
			names[ass.name] = ass;
		foreach (var n in virtualAssets.Keys)
		{
			var virt = LoadVirtualAsset(n, typ);
			if (virt != null)
			{
				// original asset overriden
				if (names.TryGetValue(n, out UnityEngine.Object over))
					allAss.Remove(over);
				allAss.Add(virt);
			}				
		}

		return allAss.ToArray();
	}

	public string[] GetAllAssetNames()
	{
		if (assetNames == null)
		{
			if (!Ensure())
				return null;
			assetNames = m_AssetBundle.GetAllAssetNames();
		}
		return assetNames;
	}

	public static int version;
	public static int level;
	[NonSerialized]
	public int locked;
	[NonSerialized]
	public int pending;
	/// <summary>
	/// Ensures that the bundle is actually loaded, kicking out any conflicting bundles in the process if needed.
	/// </summary>
	/// <param name="forname"></param>
	/// <returns>Whether the bundle is now loaded or not.</returns>
	public bool Ensure(string asset = null)
	{
		if (isVirtual)
			return true;
		if (realPath == null)
			return false;
		level++;
		locked = version;
		foreach (var dep in deps)
			Load(dep)?.Ensure();
		if (m_AssetBundle == null)
		{
			var abfn = Dir.root + realPath;
			Debug.Log("Trying to load ", name, realPath);
			m_AssetBundle = AssetBundle.LoadFromFile(abfn);
			if (m_AssetBundle == null)
			{
				// something is in the way
				GCBundles();
				m_AssetBundle = AssetBundle.LoadFromFile(abfn);
			}
		}
		level--;
		if (level == 0)
			version++;
		if (m_AssetBundle == null)
			Debug.Error("Failed to load ", name, realPath);
		return m_AssetBundle != null;
	}

	public static void GCBundles()
	{
		foreach (var b in cache.Values)
			if (b.locked < version)
				b.Unload();
	}

	public void Unload()
	{
		if (m_AssetBundle != null)
		{
			if (pending > 0)
				Debug.Error($"Unloading", name, "but it has a pending async operation!");
			m_AssetBundle.Unload(false);
			Debug.Log("Unloading ", name);
		}
		m_AssetBundle = null;
	}

	public UnityEngine.Object LoadVirtualAsset(string name, Type t)
	{
		if (!virtualAssets.TryGetValue(name, out string virt))
			return null;
		var path = Dir.root + virt;
		if (virt.EndsWith(".png") || virt.EndsWith(".jpg"))
		{
			var tex = new Texture2D(2, 2);
			tex.LoadImage(File.ReadAllBytes(path));
			if (virt.Contains("clamp"))
				tex.wrapMode = TextureWrapMode.Clamp;
			if (virt.Contains("repeat"))
				tex.wrapMode = TextureWrapMode.Repeat;
			tex.name = name;
			return tex;
		}
		if (typeof(IDumpable).IsAssignableFrom(t))
		{
			IDumpable obj;
			if (typeof(ScriptableObject).IsAssignableFrom(t))
				obj = ScriptableObject.CreateInstance(t) as IDumpable;
			else
				obj = Activator.CreateInstance(t) as IDumpable;
			var ext = virt.Substring(virt.LastIndexOf('.') + 1);
			if (obj.Unmarshal(Encoding.UTF8.GetString(File.ReadAllBytes(path)).StripBOM(), ext, name, virt))
			{
				var ret = obj as UnityEngine.Object;
				ret.name = name;
				return ret;
			}
		}
		if (typeof(TextAsset).IsAssignableFrom(t) && (virt.EndsWith(".txt") || virt.EndsWith(".lst")))
		{
			var txt = ScriptableObject.CreateInstance<TextAsset>();
			txt.name = name;
			txt.path = virt;
			txt.bytes = File.ReadAllBytes(path);
			return txt;
		}
		return null;
	}

	public UnityEngine.Object LoadAsset(string name, Type t)
	{
		var obj = LoadVirtualAsset(name, t);
		if (obj != null)
			return obj;
		if (!Ensure(name))
			return null;
		obj = m_AssetBundle.LoadAsset(name, t);
		return obj;
	}

	public IEnumerator LoadAssetAsync(string name, Type t, Action<UnityEngine.Object> cb)
	{
		var obj = LoadVirtualAsset(name, t);
		if (obj != null)
		{
			cb(obj);
			yield break;
		}	
		do
		{
			if (!Ensure(name))
				break;
			var req = m_AssetBundle.LoadAssetAsync(name, t);
			if (req == null)
				break;
			pending++;
			yield return req;
			pending--;
		}  while (m_AssetBundle == null);
		cb(obj);
	}
}

public class TextAsset : ScriptableObject
{
	public string path;
	public string text
	{
		get
		{
			return Encoding.UTF8.GetString(bytes).StripBOM();
		}
	}
	public byte[] bytes { get; set; }
}
