using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using MessagePack;
using MessagePack.LZ4;
using System.Collections;
using static Patchwork;
using System.Security.Cryptography;

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
		"!h/common",
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
	public static void LoadManifests(bool makedep = true)
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
				if (makedep)
				{
					foreach (var sab in mf.GetAllAssetBundles())
					{
						var abi = LoadedAssetBundle.Make(sab.Replace("abdata/", ""));
						foreach (var tdep in mf.GetAllDependencies(sab))
						{
							Debug.Log(" =>", sab, " depends on ", tdep);
							string dep = tdep.Replace("abdata/", "");
							if (!abi.deps.Contains(dep))
								abi.deps.Add(dep);
						}
					}
				}
			}
			ab.Unload(false);
		}
	}

	/// <summary>
	/// Rescan the directory mappings
	/// </summary>
	public static void Rescan(bool flush = true)
	{
		Debug.Log("VFS: Rescan");
		string abdata = "abdata/";
		string inabdata = Dir.root + abdata;
		if (flush)
			dc.abs.Clear();
		dc.dirLists.Clear();
		// initially map everything to self
		Debug.Log("unity3d");
		foreach (var bundle in Directory.GetFiles(inabdata, "*.unity3d", SearchOption.AllDirectories))
		{
			var rel = bundle.Replace("\\","/");
			LoadedAssetBundle.Make(rel.Substring(Dir.abdata.Length), rel.Substring(Dir.root.Length));
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
		if (settings.loadMods)
			foreach (var mod in Directory.GetDirectories(Dir.mod, "*.*", SearchOption.TopDirectoryOnly))
			{
				Debug.Log("Processing ", mod);
				var inmod = mod + "/";
				foreach (var tmpfn in Directory.GetFiles(inmod, "*.*", SearchOption.AllDirectories))
				{
					var currentfn = tmpfn.Replace("\\", "/");
					var realFn = currentfn.Substring(Dir.root.Length); // real path relative to root
					var fn = currentfn.Substring(inmod.Length); // virtualpath
					if (fn.ToLower().StartsWith("abdata/"))
						fn = fn.Substring(7);

					// may override original abdata mapping
					if (fn.EndsWith(".unity3d"))
						LoadedAssetBundle.Make(fn, realFn);

					// keep stripping last path component of the file path until we match
					// a path which is listable
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
									dc.dirLists[fn].Add(ofn.Replace("+",""));
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
							Debug.Log("adding virtual asset ", assname, "into", virtab);
							vab.virtualAssets[assname.ToLower()] = realFn;
							var nvpath = realFn.Remove(realFn.LastIndexOf('/'));
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

	public static void Save()
	{
		if (!caching) return;
#if GAME_DEBUG
		var mb = MessagePackSerializer.Serialize(dc);
		File.WriteAllBytes(cacheFile, mb);
		File.WriteAllBytes(cacheFile + ".json", MessagePackSerializer.ToJson(mb).ToBytes());
		//ExitProcess(0);
#else
		var mb = LZ4MessagePackSerializer.Serialize(dc);
		File.WriteAllBytes(cacheFile, mb);
#endif
	}

	public static string cacheFile => Dir.cache + "abinfo";

	public static bool Load()
	{
		if (File.Exists(cacheFile))
		{
			dc = LZ4MessagePackSerializer.Deserialize<DirCache>(File.ReadAllBytes(cacheFile));
			return true;
		}
		else return false;
	}

	public static bool caching => settings.abinfoCache;

	public static void Init()
	{
		Debug.Log("Vfs initializing");
		if (!caching || !Load())
		{
			Rescan();
			LoadManifests();
			if (caching)
				Save();
		}
		else LoadManifests(false);
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

	const int atlasDim = 1024;
	const int perDim = (atlasDim / 128);
	const int spriteCount = perDim * perDim;
	const TextureFormat texFmt = TextureFormat.RGBA32;
	public static Texture2D[] atlas = new Texture2D[0];
	public static Sprite[] spritePending = new Sprite[spriteCount];
	//public static Dictionary<int, Sprite> scache = new Dictionary<int, Sprite>();
	public static int spriteLen = 0;

	public static IEnumerator GetSpriteAsync(string bundle, string name, Action<Sprite> ret)
	{
		var ab = LoadedAssetBundle.Load(bundle);
		if (ab != null)
		{
			yield return ab.LoadAssetAsync(name, typeof(Texture2D), (tex) =>
			{
				var t = tex as Texture2D;
				if (t)
					ret(Sprite.Create(t, new Rect(0f, 0f, (float)t.width, (float)t.height), new Vector2(0.5f, 0.5f)));
				else
					ret(null);
			});
		}
		ret(null);
	}

	public static bool GetSprite(string bundle, string name, out Sprite ret)
	{
		ret = null;
		var lab = LoadedAssetBundle.Load(bundle);
		if (lab == null)
			return false;
		if (!caching)
		{
			var t = LoadedAssetBundle.Load(bundle)?.LoadAsset(name, typeof(Texture2D)) as Texture2D;
			if (t == null)
				return false;
			ret = Sprite.Create(t, new Rect(0f, 0f, (float)t.width, (float)t.height), new Vector2(0.5f, 0.5f));
			return true;
		}
		Debug.Log("Trying to get sprite for ", bundle, name);
		if (lab.cachedSprites.TryGetValue(name, out int idx))
		{
			/*if (scache.TryGetValue(idx, out Sprite sc))
			{
				if (sc)
					sc = Sprite.Instantiate(sc);
				return sc;
			}*/
			Debug.Log("potential cache hit at ", idx, atlas.Length);
			int atlasno = (idx / spriteCount);
			int off = idx % spriteCount;
			// load atlases up to that count
			for (int i = atlas.Length; i <= atlasno; i++)
			{
				var atn = Dir.cache + "abinfo" + i;
				if (File.Exists(atn))
				{
#if USE_BC7
					var fmt = TextureFormat.BC7;
					var dbuf = new byte[atlasDim * atlasDim];
#elif USE_DXT
					var fmt = TextureFormat.DXT5;
					var dbuf = new byte[atlasDim * atlasDim];
#else
					var fmt = TextureFormat.RGBA32;
					var dbuf = new byte[4 * atlasDim * atlasDim];
#endif
					Texture2D at = new Texture2D(atlasDim, atlasDim, fmt, false);
					at.LoadRawTextureData(Ext.LZ4Decompress(File.ReadAllBytes(atn), dbuf));
					at.Apply(updateMipmaps: false, makeNoLongerReadable: true);
					if (atlas.Length <= i)
						Array.Resize(ref atlas, i + 1);
					Texture2D.DontDestroyOnLoad(at);
					atlas[i] = at;
				} else
				{
					Debug.Log("This cache index ",idx," doesn't exist but should! (atlasno ",i,")", atn);
					break;
				}
			}
			if (atlasno < atlas.Length)
			{
				Debug.Log("Sprite within atlas range");
				int x = (off % perDim) * 128;
				int y = (off / perDim) * 128;
				var st = Sprite.Create(atlas[atlasno], new Rect(x, y, 128, 128), new Vector2(0.5f, 0.5f));
				//Sprite.DontDestroyOnLoad(st);
				//scache[idx] = st;
				ret = st;// Sprite.Instantiate(st);
				return true;
			}
			if (atlasno == atlas.Length)
			{
				if (off < spriteLen)
				{
					Debug.Log("Found the sprite in pending set");
					return spritePending[off] ? Sprite.Instantiate(spritePending[off]) : null;
				}
			}
			Debug.Error("Corrupted sprite cache. This shouldn't happen.",atlasno,atlas.Length,off,spriteLen);
		}
		Debug.Log("cache miss. we need to cache the sprite afresh.");
		var tex = LoadedAssetBundle.Load(bundle)?.LoadAsset(name, typeof(Texture2D)) as Texture2D;
		int localidx = spriteLen;
		lab.cachedSprites[name] = localidx + atlas.Length * spriteCount;
		spriteLen = localidx + 1;
		if (tex != null)
		{

		}
		/*scache[idx] = tex ? Sprite.Create(tex, new Rect(0f, 0f, (float)tex.width, (float)tex.height), new Vector2(0.5f, 0.5f)) : null;
		if (tex != null)
			Sprite.DontDestroyOnLoad(scache[idx]);
		spritePending[localidx] = scache[idx];
		if (tex != null)
			ret = Sprite.Instantiate(scache[idx]);
		*/
		ret = spritePending[localidx] = tex ? Sprite.Create(tex, new Rect(0f, 0f, (float)tex.width, (float)tex.height), new Vector2(0.5f, 0.5f)) : null;
		if (ret != null)
			Sprite.DontDestroyOnLoad(ret);
		// swap out the pending ones into a new texture if full
		if (spriteLen == spriteCount)
		{
			Debug.Log("sprite atlas full, flushing");
			Texture2D at = new Texture2D(atlasDim, atlasDim, TextureFormat.RGBA32, false);

			RenderTexture tmp = RenderTexture.GetTemporary(128, 128);
			tmp.filterMode = FilterMode.Point;
			RenderTexture.active = tmp;
			for (int off = 0; off < spriteCount; off++)
			{
				if (spritePending[off] == null)
					continue;
				int x = (off % perDim) * 128;
				int y = (off / perDim) * 128;
				Graphics.Blit(spritePending[off].texture, tmp); // blit and stretch the original to our 128x128 render target
				at.ReadPixels(new Rect(0, 0, 128, 128), x, y);
				spritePending[off] = null;
			}
			at.Apply();
			RenderTexture.active = null;
			int atno = atlas.Length;
			Array.Resize(ref atlas, atno + 1);
			atlas[atno] = at;
#if GAME_DEBUG
			File.WriteAllBytes(Dir.cache + "abinfo" + atno + ".png", at.EncodeToPNG());
#endif

#if USE_BC7
			var rawbuf = at.GetRawTextureData();
			var bc7buf = new byte[rawbuf.Length / 4];
			unsafe {
				fixed (byte* pbc7buf = bc7buf)
				{
					fixed (byte* prawbuf = rawbuf)
						bc7_compress(new IntPtr(pbc7buf), new IntPtr(prawbuf), atlasDim, atlasDim);
				}
			}
			rawbuf = bc7buf;
#elif USE_DXT
			at.Compress(false);
			var rawbuf = at.GetRawTextureData();
#else
			var rawbuf = at.GetRawTextureData();
#endif
			File.WriteAllBytes(Dir.cache + "abinfo" + atno, Ext.LZ4Compress(rawbuf));

			spriteLen = 0;
			Save();
		}
		return ret != null;
	}

	public static bool Repack(Stream input, Stream output, bool randomize = false, int lz4blockSize = 128 * 1024)
	{
		var baseStart = output.Position;
		var r = new BinaryReader(input, Encoding.ASCII);
		var w = new BinaryWriter(output, Encoding.ASCII);

		var format = r.GetString();
		if (format != "UnityFS")
			return false;
		w.Put(format);

		var gen = r.GetInt();
		if (gen != 6)
			return false;
		w.Put(gen);

		w.Put(r.GetString());
		w.Put(r.GetString());

		// defer
		var infoPos = w.BaseStream.Position;
		w.BaseStream.Position += 16; // bundlesize + metacomp + metauncomp

		var bundleSize = r.GetLong();
		var metaCompressed = r.GetInt();
		var metaUncompressed = r.GetInt();
		var flags = r.GetInt();
		w.Put(0x43);
		var dataPos = r.BaseStream.Position;

		if ((flags & 0x80) != 0)
			r.BaseStream.Position = bundleSize - metaCompressed;
		else
			dataPos += metaCompressed;

		byte[] metabuf = null;
		switch (flags & 0x3f)
		{
			case 3:
			case 2:
				metabuf = Ext.LZ4Decompress(r.ReadBytes(metaCompressed), 0, metaCompressed, metaUncompressed);
				break;
			case 0:
				metabuf = r.ReadBytes(metaUncompressed);
				break;
			default:
				return false;
		}

		r.BaseStream.Position = dataPos;
		var meta = new BinaryReader(new MemoryStream(metabuf), Encoding.ASCII);
		var newmeta = new BinaryWriter(new MemoryStream(), Encoding.ASCII);
		newmeta.BaseStream.Position += 16 + 4; // +4 for pending.Length
		meta.BaseStream.Position += 16;
		int nblocks = meta.GetInt();
		List<byte[]> pending = new List<byte[]>();
		for (var i = 0; i < nblocks; i++)
		{
			var origSize = meta.GetInt();
			var compSize = meta.GetInt();
			var blockFlags = meta.GetShort();
			var block = r.ReadBytes(compSize);
			if (blockFlags == 0x40 || blockFlags == 2 || blockFlags == 3)
			{
				if (blockFlags != 0x40)
					block = Ext.LZ4Decompress(block, 0, compSize, origSize);
				for (int pos = 0; pos < block.Length; pos += lz4blockSize)
				{
					var orig = Math.Min(lz4blockSize, block.Length - pos);
					var newblock = Ext.LZ4Compress(block, pos, orig);
					newmeta.Put(orig);
					newmeta.Put(newblock.Length); ;
					newmeta.Put((short)3);
					pending.Add(newblock);
				}
			}
			else
			{
				newmeta.Put(origSize);
				newmeta.Put(compSize);
				newmeta.Put(blockFlags);
				pending.Add(block);
			}
		}

		//Console.WriteLine(pending.Count);
		int nfiles = meta.GetInt();
		newmeta.Put(nfiles);
		var rng = new RNGCryptoServiceProvider();
		for (int i = 0; i < nfiles; i++)
		{
			newmeta.Put(meta.GetLong());
			newmeta.Put(meta.GetLong());
			newmeta.Put(meta.GetInt());
			var name = meta.GetString();
			if (randomize)
			{
				var rnbuf = new byte[16];
				rng.GetBytes(rnbuf);
				name = "CAB-" + string.Concat(rnbuf.Select((x) => ((int)x).ToString("X2")).ToArray()).ToLower();
			}
			newmeta.Put(name);
		}
		newmeta.BaseStream.Position = 16;
		newmeta.Put(pending.Count);
		var newmetabuf = (newmeta.BaseStream as MemoryStream).ToArray();
		var newmetabufc = Ext.LZ4Compress(newmetabuf, 0, newmetabuf.Length);
		w.Write(newmetabufc);
		foreach (var buf in pending)
			w.Write(buf);
		var endpos = w.BaseStream.Position;
		var bundlesize = endpos - baseStart;
		w.BaseStream.Position = infoPos;
		w.Put(bundlesize);
		w.Put(newmetabufc.Length);
		w.Put(newmetabuf.Length);
		output.Position = endpos;
		return true;
	}

}

#if GAME_DEBUG
[MessagePackObject(true)]
#else
[MessagePackObject]
#endif
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
	[Key(5)]
	public Dictionary<string, int> cachedSprites = new Dictionary<string, int>();
	[Key(6)]
	public List<string> virtualBundles = new List<string>();

	[IgnoreMember]
	public AssetBundle[] loadedVirtualBundles;

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
		GCBundles();
		return true;
	}


	/// <summary>
	/// Get or create new empty ab
	/// </summary>
	/// <param name="name"></param>
	/// <returns></returns>
	public static LoadedAssetBundle Make(string name, string real = null)
	{
		var abname = name.Replace("+", "");
		Debug.Log("Registering", name, " as ", abname, real);
		LoadedAssetBundle ab;
		if (!cache.TryGetValue(abname, out ab))
		{
			ab = new LoadedAssetBundle(abname);
			ab.realPath = real;
			cache[abname] = ab;
			if (abname != name)
				Debug.Error("First registered name shouldn't be unsafe!",name,real);
		}
		else
		{
			// virtual
			if (abname != name)
			{
				Debug.Log("adding as virtual, ", real);
				ab.virtualBundles.Add(real);
			} else
			{
				Debug.Log("already exists", ab.realPath);
				ab.realPath = real ?? ab.realPath;
			}
		}
		return ab;
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
		Debug.Log("Get bundle ", name);
		if (cache.TryGetValue(name, out LoadedAssetBundle ab))
		{
			Debug.Log("got ab at ", ab.realPath);
			return ab;
		}
		Debug.Error("Untracked bundle ", name, "requested");
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
			Debug.Error("Load failed; the bundle ",name," is not tracked.");
			return null;
		}

		if (ab.realPath == null)
		{
			Debug.Log("real path missing");
			return null;
		}
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
			List<string> tmp = virtualAssets.Keys.ToList();
			if (m_AssetBundle != null)
				tmp.AddRange(m_AssetBundle.GetAllAssetNames().Select((x) => Path.GetFileNameWithoutExtension(x).ToLower()));
			var tmp2 = tmp.Distinct().ToList();
			tmp2.Sort();
			assetNames = tmp2.ToArray();
			/*foreach (var an in virtualAssets.Keys)
			{
				tmp.Remove(an);
				tmp.Add(an);
			}
			assetNames = tmp.ToArray();*/
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
			m_AssetBundle = TryLoadAB(realPath);
			if (m_AssetBundle != null)
			{
				loadedVirtualBundles = virtualBundles.Select((x) => TryLoadAB(x)).ToArray();
				for (int i = 0; i < loadedVirtualBundles.Length; i++)
				{
					var vb = loadedVirtualBundles[i];
					if (vb == null) continue;
					foreach (var ass in vb.GetAllAssetNames().Select((x) => Path.GetFileNameWithoutExtension(x).ToLower()))
					{
						if (!virtualAssets.ContainsKey(ass))
						{
							Debug.Log("@virt asset from bundle ", ass, i);
							virtualAssets[ass] = "@" + i;
						}
					}
				}
			}
			else
			{
				// last resort
				GCBundles();
				m_AssetBundle = TryLoadAB(realPath);
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
			{
				b.Unload();
			}
	}

	public AssetBundle TryLoadAB(string path)
	{
		Debug.Log("trying to load ", path);
		var ab = AssetBundle.LoadFromFile(Dir.root + path);
		if (ab != null)
			return ab;
		var key = Ext.HashToString(path.ToBytes()).Substring(0, 12);
		var alt = Dir.cache + key + ".unity3d";

		ab = AssetBundle.LoadFromFile(alt);
		if (ab != null)
			return ab;


		if (!File.Exists(alt))
		{
			using (var f = File.OpenRead(realPath))
			{
				using (var fo = File.Create(alt))
				{
					Vfs.Repack(f, fo, true);
				}
			}
		}

		Debug.Log("no dice, trying alternat path at ", alt);
		var ret = AssetBundle.LoadFromFile(alt);
		if (ret == null)
		{
			Debug.Log("load failed");
		}
		return ret;
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
			foreach (var vab in loadedVirtualBundles)
				if (vab != null)
					vab.Unload(false);
			Debug.Log("Unloading ", name);
		}
		m_AssetBundle = null;
		loadedVirtualBundles = null;
	}

	public object LoadFromVirtualBundles(string name, Type t)
	{
		if (!settings.loadUnsafe)
			return null;
		Debug.Log("trying virtual load from", this.name, name);
		if (!virtualAssets.TryGetValue(name, out string virt))
		{
			Debug.Log("Virtual asset doesn't exist");
			return null;
		}
		if (virt.StartsWith("@"))
		{
			int idx = int.Parse(virt.Substring(1));
			if (loadedVirtualBundles[idx] != null)
			{
				Debug.Log(name, " loaded from virtual bundle", virtualBundles[idx]);
				return LoadAssetWrapped(loadedVirtualBundles[idx], name, t);
			}
		}
		Debug.Log("no virtual asset");
		return null;
	}

	public object LoadVirtualAsset(string name, Type t)
	{
		if (!virtualAssets.TryGetValue(name.ToLower(), out string virt))
			return null;
		Debug.Log("Trying to load virtual asset from ", this.name, name, virt);
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
		Debug.Log("No virtual asset loaded");
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
		var ass = LoadFromVirtualBundles(name, t) ?? LoadAssetWrapped(m_AssetBundle, name, t);
		if (ass == null)
		{
			Debug.Error("Load of ", this.name, name, "failed");
		}
		return ass;
	}

	public static object LoadAssetWrapped(AssetBundle ab, string name, Type t)
	{
		if (ab == null) return null;
		return TextAsset.Wrap<object>(ab.LoadAsset(name, TextAsset.Unwrap(t)));
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
			goto @out;
		obj = LoadFromVirtualBundles(name, t);
		if (obj != null)
			goto @out;
		var req = m_AssetBundle.LoadAssetAsync(name, TextAsset.Unwrap(t));
		if (req == null)
			goto @out;
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
@out:
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
