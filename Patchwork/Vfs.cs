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
		"action/actioncontrol",
		"action/fixchara",
		"action/list/chara",
		"action/list/clubinfo",
		"action/list/event",
		"action/list/fixchara",
		"action/list/fixevent",
		"!action/list/sound/bgm",
		"!action/list/sound/se/action",
		"!action/list/sound/se/env",
		"!action/list/sound/se/footstep",
		"action/list/talklookbody",
		"action/list/talklookneck",
		"action/list/waitpoint/non",
		"action/playeraction",
		"adv/eventcg",
		"adv/faceicon/list",
		"adv/scenario",
		"communication",
		"etcetra/list/config",
		"etcetra/list/exp",
		"etcetra/list/nickname",
		"!list/characustom",
		"map/actionpoint",
		"map/list/calcgateinfo",
		"map/list/mapinfo",
		"map/list/navigationinfo",
		"!map/sound",
		"map/waitpoint",
		"sound/data/systemse/brandcall",
		"sound/data/systemse/titlecall",
		"studio/info",

		"!h/list",
		"!action/list/motionvoice",
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
			Debug.Log("scanning for manifest in ", man);
			var mf = ab.LoadAsset<AssetBundleManifest>("AssetBundleManifest");
			if (mf != null)
			{
				AssetBundleManager.ManifestBundlePack["abdata/"+fn] = new AssetBundleManager.BundlePack() { AssetBundleManifest = mf };
				foreach (var sab in mf.GetAllAssetBundles())
				{
					
					var abi = LoadedAssetBundle.Make(sab.Replace("abdata/",""));
					foreach (var tdep in mf.GetAllDependencies(sab))
					{
						Debug.Log(" =>", sab, " depends on ", tdep);
						string dep = tdep.Replace("abdata/","");
						if (!abi.deps.Contains(dep))
							abi.deps.Add(dep);
					}
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
		Debug.Log("VFS: Rescan");
		string abdata = "abdata/";
		string inabdata = Dir.root + abdata;
		dc.abs.Clear();
		dc.dirLists.Clear();
		// initially map everything to self
		Debug.Log("unity3d");
		foreach (var bundle in Directory.GetFiles(inabdata, "*.unity3d", SearchOption.AllDirectories))
		{
			var rel = bundle.Replace("\\","/");
			LoadedAssetBundle.Make(rel.Substring(Dir.abdata.Length)).realPath = rel.Substring(Dir.root.Length);
		}
		Debug.Log("dirlists");
		Dictionary<string,bool> lhash = new Dictionary<string, bool>();
		// Now scan for listable bundles in abdata
		foreach (var ld in listable)
		{
			var lld = ld.Replace("!", "");
			lhash[lld] = lld != ld;
			if (!Directory.Exists(Dir.abdata + lld)) continue;
			dc.dirLists[lld] = new List<string>();
			foreach (var blist in Directory.GetFiles(Dir.abdata + lld, "*.unity3d", ld != lld ? SearchOption.TopDirectoryOnly : SearchOption.AllDirectories))
			{
				var fn = blist.Replace("\\", "/").Substring(Dir.abdata.Length);
				dc.dirLists[lld].Add(fn);

				// if recursive, walk up and populate dirlists
				int idx = fn.LastIndexOf('/');
				if (ld == lld && idx >= 0)
				{
					string td = fn.Remove(idx);
					while (td != lld)
					{
						if (!dc.dirLists.TryGetValue(td, out List<string> tdl))
							tdl = dc.dirLists[td] = new List<string>();
						tdl.Add(fn);
						td = td.Remove(td.LastIndexOf('/'));
					}
				}
			}
		}
		Debug.Log("mods");
		// now scan mods, those will simply override the tables above
		foreach (var mod in Directory.GetDirectories(Dir.mod, "*.*", SearchOption.TopDirectoryOnly))
		{
			Debug.Log("Processing ", mod);
			var inmod = mod + "/";
			foreach (var bundle in Directory.GetFiles(inmod, "*.*", SearchOption.AllDirectories))
			{
				var bd = bundle.Replace("\\", "/");
				var fn = bd.Substring(Dir.root.Length);
				var ffn = fn;
//				Debug.Log("modfiles ", bundle, bd, fn);

				// may override original abdata mapping
				if (bundle.EndsWith(".unity3d"))
					LoadedAssetBundle.Make(bd.Substring(inmod.Length)).realPath = fn;		

				// keep stripping last path component of the file path until we match
				// a path which is listable
				fn = bd.Substring(inmod.Length); // remove modpath -> abesque
				var ofn = fn;
				int ind;
				int stripped = 0;
				while ((ind = fn.LastIndexOf('/')) >= 0)
				{
					fn = fn.Remove(ind); // is directory now
					 //	Debug.Log("listable lookup", fn, ofn);
					var virtab = ofn.Remove(ofn.LastIndexOf('/')) + ".unity3d";
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
							// add to dir listings
							if ((!norecurse || stripped == 0) || !dc.dirLists[fn].Contains(virtab))
							{
								dc.dirLists[fn].Add(virtab);
							}
						}
					}
					if (fn != "" && stripped == 0 && !ofn.EndsWith("*.unity3d"))
					{
						// create a virtual bundle
						var assname = RemoveExt(Path.GetFileName(ofn));
						var vab = LoadedAssetBundle.Make(virtab);
						vab.virtualAssets[assname] = ffn;
						var nvpath = ffn.Remove(ffn.LastIndexOf('/'));
						Debug.Log(assname, virtab, fn, nvpath, vab.realPath);
						if (vab.realPath == null && nvpath != vab.realPath)
							vab.realPath = nvpath;
						else
							Debug.Log("Not overriding ", vab.realPath, " with ", nvpath);
					}
					stripped++;
				}
			}
		}
	}

	public static string RemoveExt(string name)
	{
		int pos = name.LastIndexOf('.');
		if (pos < 0) return name;
		return name.Remove(pos);
	}

	public static string GetExt(string name)
	{
		int pos = name.LastIndexOf('.');
		if (pos < 0) return null;
		return name.Substring(pos+1);
	}

	public static DirCache dc = new DirCache();

	public static void Init()
	{
		Rescan();
		LoadManifests();
		var mb = MessagePackSerializer.Serialize(dc);
		File.WriteAllBytes("dc.json", MessagePackSerializer.ToJson(mb).ToBytes());
	}

	public static bool initialized;
	public static void CheckInit()
	{
		if (initialized)
			return;
		Init();
		initialized =  true;
	}

	public static List<string> GetAssetBundleNameListFromPath(string path, bool recurse)
	{
		CheckInit();
		Debug.Log("Asked for bundle list on ", path);
		if (path.EndsWith("/"))
			path = path.Remove(path.Length - 1);
#if GAME_DEBUG
		if (recurse && listable.Contains("!" + path))
		{
			Debug.Error("Recurse/norecurse mismatch for ", path);
		}
#endif
		if (dc.dirLists.TryGetValue(path, out List<string> list))
			return list;
		Debug.Error("Unable to get bundle list from ", path, recurse);
		return new List<string>();
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

[MessagePackObject(true)]
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

	[IgnoreMember]
	public AssetBundle m_AssetBundle;

	[IgnoreMember]
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
		Debug.Log("Registering", name);
		if (cache.TryGetValue(name, out LoadedAssetBundle ab))
			return ab;
		return cache[name] = new LoadedAssetBundle(name);
	}

	public static void CheckInit()
	{
		Vfs.CheckInit();
	}

	/// <summary>
	/// Get existing ab
	/// </summary>
	/// <param name="name"></param>
	/// <returns></returns>
	public static LoadedAssetBundle Get(string name)
	{
		CheckInit();
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
		CheckInit();
		Debug.Log("Loading bundle ", name, "for", forasset);
		var ab = Get(name);
		if (ab == null)
		{
			Debug.Error("Load failed; this bundle is not tracked.");
			return null;
		}

		if (ab.realPath == null)
			return null;
		return ab;
	}

	public object[] LoadAllAssets(Type typ)
	{
		Debug.Stack("Loading all assets of type ", typ, " in ", name);
		if (!Ensure())
			return null;
		List<object> allAss = new List<object>();
		if (m_AssetBundle != null)
			foreach (var obj in m_AssetBundle.LoadAllAssets(TextAsset.Unwrap(typ)).Select(TextAsset.Wrap<UnityEngine.Object>))
			{
				// virtuals assets of same name always remove the object from listing.
				if (obj != null && !virtualAssets.ContainsKey(obj.name))			
					allAss.Add(obj);
				else Debug.Log("vanilla", obj.name, " nuked by virtual asset");
			}

		Debug.Log("Found ", allAss.Count, " base assets");
		foreach (var n in virtualAssets.Keys)
		{
			var virt = LoadVirtualAsset(n, typ);
			if (virt != null)
			{
				Debug.Log("Adding virtual asset", n, typ);
				// original asset overriden
				allAss.Add(virt);
			}				
		}

		return allAss.ToArray();
	}

	public string[] GetAllAssetNames()
	{
		CheckInit();
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
	[IgnoreMember]
	public int locked;
	[IgnoreMember]
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
			Debug.Log("Trying to load ", name, "real=", realPath);
			m_AssetBundle = AssetBundle.LoadFromFile(abfn);
			if (m_AssetBundle == null)
			{
				if (!abfn.StartsWith(Dir.cache))
				{
					var key = Ext.HashToString(realPath.ToBytes()).Substring(0, 12);
					var alt = Dir.cache + key + ".unity3d";
					if (!File.Exists(alt))
					{
						using (var f = File.OpenRead(abfn))
						{
							using (var fo = File.Create(alt))
							{
								FixCAB(fo, f, key);
							}
						}
					}
					Debug.Log("Trying to fix CAB conflict ", abfn, alt);
					m_AssetBundle = AssetBundle.LoadFromFile(alt);
					if (m_AssetBundle != null) {
						realPath = alt.Substring(Dir.cache.Length);
					} else
					{
						Debug.Log("CAB fix failed");
					}
				}

				if (m_AssetBundle == null)
				{
					GCBundles();
					m_AssetBundle = AssetBundle.LoadFromFile(abfn);
				}
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

	public bool FixCAB(Stream output, Stream input, string key)
	{
		var bytes = new byte[256];
		input.Read(bytes, 0, 256);
		int pos = -1;
		for (int i = 0; i < 256; i++)
			if (bytes[i] == 'C' && bytes[i + 1] == 'A' && bytes[i + 2] == 'B' && bytes[i + 3] == '-')
			{
				pos = i;
				break;
			}
		if (pos < 0) return false;
		var hash = System.Security.Cryptography.MD5.Create();
		output.Write(bytes, 0, pos + 4); // CAB- + move past
		var cabstr = string.Join("", hash.ComputeHash(key.ToBytes()).Take(16).Select((x) => x.ToString("x2")).ToArray()).ToBytes();
		output.Write(cabstr, 0, cabstr.Length);
		var rem = pos + 4 + cabstr.Length;
		output.Write(bytes, rem, bytes.Length - rem);
		input.CopyTo(output);
		return true;
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

	public object LoadVirtualAsset(string name, Type t)
	{
		if (!virtualAssets.TryGetValue(name, out string virt))
			return null;
		Debug.Log("Trying to load virtual asset", name);
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
			if (obj.Unmarshal(Encoding.UTF8.GetString(File.ReadAllBytes(path)).StripBOM(), Vfs.GetExt(virt), name, virt))
			{
				if (obj is UnityEngine.Object)
					(obj as UnityEngine.Object).name = name;
				return obj;
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

	public object LoadAsset(string name, Type t)
	{
		Debug.Stack("Loading ", this.name, name, t);
		var obj = LoadVirtualAsset(name, t);
		if (obj != null)
			return obj;
		if (!Ensure(name))
			return null;
		obj = LoadAssetWrapped(name, t);
		return obj;
	}

	public object LoadAssetWrapped(string name, Type t)
	{
		return TextAsset.Wrap<object>(m_AssetBundle.LoadAsset(name, TextAsset.Unwrap(t)));
	}

	public IEnumerator LoadAssetAsync(string name, Type t, Action<object> cb)
	{
		Debug.Stack("Async Loading ", name, t);
		var obj = LoadVirtualAsset(name, t);
		if (obj != null)
		{
			cb(obj);
			yield break;
		}
		if (!Ensure(name))
			goto err;
		var req = m_AssetBundle.LoadAssetAsync(name, TextAsset.Unwrap(t));
		if (req == null)
			goto err;
		pending++;
		yield return req;
		pending--;
		obj = req.asset;
		if (obj == null && m_AssetBundle == null)
		{
			Debug.Log("Falling back to sync load for ", name, " due to pulled rug.");
			Ensure(name);
			// fall back to sync
			obj = m_AssetBundle.LoadAsset(name, TextAsset.Unwrap(t));
		}
err:
		cb(TextAsset.Wrap<object>(obj));
	}
}

public class TextAsset : ScriptableObject
{
	public string path;
	public string _text;
	public string text
	{
		get
		{
			if (_text == null)
				_text = Encoding.UTF8.GetString(bytes).StripBOM();
			return _text;
		}
	}
	public byte[] bytes { get; set; }

	public static T Wrap<T>(object o) where T: class
	{
		if (o == null)
			return null;
		var to = o as UnityEngine.TextAsset;
		if (to != null)
		{
			var so = CreateInstance<TextAsset>();
			so._text = to.text;
			so.bytes = to.bytes;
			return so as T;
		}
		return o as T;
	}

	public static Type Unwrap(Type t)
	{
		if (t == typeof(TextAsset))
			return typeof(UnityEngine.TextAsset);
		return t;
	}
}
