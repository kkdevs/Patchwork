using UnityEngine;
using Illusion.Extensions;
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
using System;

namespace Patchwork
{
	// Higher level vfs - handles unioning, dumping and serialization interface.
	// caches file access to avoid hitting icall
	public class Cache
	{
		public static SaveFrameAssist saveFrameAssist;
		public static string dumpdir => UserData.Path + "/csv/";
		public static string dumpdirCanon => Path.GetFullPath(dumpdir).ToLower();

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
			{
				Debug.Log($"[CACHE] Loading LST {bundle}/{asset}");
				return true;
			}
			var ta = CommonLib.LoadAsset<TextAsset>(bundle, asset);
			if (ta == null)
				return false;
			var text = ta.text;
			if (text == null)
				return false;
			YS_Assist.GetListString(text, out data);
			if (Program.settings.dumpAssets && !File.Exists(ABPath(bundle, asset, "csv")))
			{
				Debug.Log($"[CACHE] Dumped LST {bundle}/{asset}");
				var ex = New<ExcelData>();
				ex.Import(CSV.ParseTSV(text));
				Save(ex, bundle, asset);
			}
			return true;
		}

		public static T New<T>() where T : class, new()
		{
			return New(typeof(T)) as T;
		}
		public static object New(Type t)
		{
			if (typeof(ScriptableObject).IsAssignableFrom(t))
			{
				return ScriptableObject.CreateInstance(t);
			}
			else
			{
				return Activator.CreateInstance(t);
			}
		}

		public static string LoadString(string bundle, string asset, string suffix = "csv")
		{
			return LoadString(ABPath(bundle, asset, suffix));
		}
		// Load plain string from cache
		public static string LoadString(string path)
		{
			try
			{
				return System.Text.Encoding.UTF8.GetString(File.ReadAllBytes(path)).StripBOM();
			}
			catch
			{
				return null;
			}
		}

		// Save plain string to cache
		public static bool SaveString(string buf, string bundle, string asset, string suffix = "csv")
		{
			try
			{
				var file = ABPath(bundle, asset, suffix, true); // makes the folder too
				File.WriteAllBytes(file, buf.AddBOM().ToBytes());
				return true; // XXX
			}
			catch
			{
				return false;
			}
		}

		/*public static T Load<T>(string bundle, string asset, string ext = "csv") where T : class, IDumpable, new()
		{
			return Load(bundle, asset, typeof(T), ext) as T;
		}*/

		public static T Load<T>(string path, string ext = "csv") where T : class, IDumpable, new()
		{
			return Load(GetPath(path), typeof(T), ext) as T;
		}

		public static object Load(string bundle, string asset, Type typ, string ext = "csv")
		{
			return Load(GetPath(ABPath(bundle, asset, ext)), typ, ext);
		}
		// Load marhsalled type from csv
		public static object Load(string path, Type typ, string ext = null)
		{
			var str = Cache.LoadString(path);
			if (str == null)
				return null;
			var ex = New(typ) as IDumpable;
			if (ext == null)
				ext = ex.GetFileExt();
			if (!ex.Unmarshal(str, ext))
				return null;
			return ex;
		}

		// Save marshalled type to csv
		public static bool Save(object o, string bundle, string asset)
		{
			var ex = o as IDumpable;
			var ext = ex.GetFileExt();
			// already saved
			if (!Program.settings.dumpAssets || File.Exists(ABPath(bundle, asset, ext)))
				return true;
			if (ex == null)
				return false;
			var str = ex.Marshal();
			if (str == null)
				return false;
			return Cache.SaveString(str, bundle, asset, ext);
		}

		// Load generic asset. Serializable assets are re-routed to/from CSV.
		// Returns true if we *handle* the request, regardless of success.
		public static bool Asset(string bundle, string asset, System.Type type, string manifest, out AssetBundleLoadAssetOperation res)
		{
			res = null;
			Debug.Log($"[CACHE] Loading translated asset {bundle} {asset} {type.Name}");
			if (asset == null)
				return false;
#if !USE_OLD_ABM
			var fakepath = LoadedAssetBundle.basePath + bundle.Substring(0, bundle.Length - 8) + "/" + asset;
			if (type == typeof(Texture2D))
			{
				var pngpath = GetPath(fakepath + ".png");			
				Texture2D tex = null;
				if (pngpath != null)
					(tex = new Texture2D(2, 2)).LoadImage(File.ReadAllBytes(pngpath));
				else
				{
					var jpgpath = GetPath(fakepath + ".jpg");
					if (jpgpath != null)
						(tex = new Texture2D(2, 2)).LoadImage(File.ReadAllBytes(jpgpath));
				}
				if (tex != null)
				{
					if (fakepath.Contains("clamp"))
						tex.wrapMode = TextureWrapMode.Clamp;
					else if (fakepath.Contains("repeat"))
						tex.wrapMode = TextureWrapMode.Repeat;
					res = new AssetBundleLoadAssetOperationSimulation(tex);
					return true;
				}
			}
#endif
			if (!typeof(IDumpable).IsAssignableFrom(type))
				return false;
			if (Program.settings.fetchAssets)
			{
				var obj = Load(bundle, asset, type) as UnityEngine.Object;
				if (obj != null)
				{
					Debug.Log($"[CACHE] Loading CSV {bundle}/{asset}");
					res = new AssetBundleLoadAssetOperationSimulation(obj);
					return true;
				}
			}

			res = AssetBundleManager._LoadAsset(bundle, asset, type, manifest);

			if (Program.settings.dumpAssets)
			{
				if (!res.IsEmpty() && !File.Exists(ABPath(bundle, asset, "csv")))
				{
					Debug.Log($"[CACHE] Saving CSV {bundle}/{asset}");
					Save(res.GetAsset<UnityEngine.Object>(), bundle, asset);
				}
			}
			return true;
		}

		public static string Base(ChaInfo who, string typ = "oo")
		{
			if (typ == "oo")
			{
				return (who.sex == 0 ? Program.settings.ooMale : Program.settings.ooFemale) + ".unity3d";
			}
			else
			{
				return (who.sex == 0 ? Program.settings.mmMale : Program.settings.mmFemale) + ".unity3d";
			}
			return null;
		}


		public static List<string[,]> LoadMultiLst(string bundledir, string asset)
		{
			var res = new List<string[,]>();
			//asset = asset.ToLower();
			List<string> bundles = CommonLib.GetAssetBundleNameListFromPath(bundledir, false);
			foreach (var bn in bundles)
			{
				string[,] entry = null;
				if (Program.settings.fetchAssets)
					if (CSV.LoadLst(bn, asset, out entry)) {
						res.Add(entry);
						continue;
					}
				//var ta = LoadedAssetBundle.Load(bn)?.LoadAsset(asset, typeof(TextAsset)) as TextAsset;
				var ta = CommonLib.LoadAsset<TextAsset>(bn, asset);
				if (ta == null)
					continue;
				var ex = ScriptableObject.CreateInstance<ExcelData>();
				ex.Import(CSV.ParseTSV(ta.text));
				Save(ex, bn, asset);
				CSV.LoadLst(ex, out entry);
				res.Add(entry);
			}
			Script.registry[asset] = res;
			return res;
		}

		public static List<string> GetFiles(string path, string mask = "*.*", bool nobase = false)
		{
#if USE_OLD_ABM
			return Directory.GetFiles(path, mask).ToList();
#else
			return GetFilesOrDirs(path, mask, false, nobase);
#endif
		}
		public static List<string> GetDirectories(string path, string mask = "*.*", bool nobase = false)
		{
#if USE_OLD_ABM
			return Directory.GetDirectories(path, mask).ToList();
#else
			return GetFilesOrDirs(path, mask, true, nobase);
#endif
		}

#if !USE_OLD_ABM
		// Expects canonical path!
		public static string ExtractABPath(string path, out string addy)
		{
			var pathl = path.ToLower();
			addy = null;
			if (!pathl.StartsWith(LoadedAssetBundle.basePathCanon))
			{
				// eventually there's going to be more than /csv
				if (pathl.StartsWith(dumpdirCanon))
				{
					var sb = path.Substring(dumpdirCanon.Length);
					addy = dumpdirCanon + sb;
					return sb;
				}
				return null;
			}
			return path.Substring(LoadedAssetBundle.basePathCanon.Length);
		}

		// Expects canonical path!
		public static Dictionary<string, List<string>> dirCache = new Dictionary<string, List<string>>();
		// List union of files or directories
		public static List<string> GetFilesOrDirs(string path, string mask, bool isdirs = false, bool nobase = false)
		{
			//Debug.Log($"[CACHE] GetFilesOrDirs {path} {mask} {isdirs} {nobase}");
			//Debug.Log(dumpdirCanon);
			//Debug.Log(LoadedAssetBundle.basePathCanon);
			var key = nobase.ToString() + path;
			if (dirCache.TryGetValue(key, out List<string> cached))
				return cached;

			var res = new List<string>();
			var subpath = ExtractABPath(path, out string addy);
			// actually not abdata
			if (subpath == null)
			{
				if (isdirs)
					return Directory.GetDirectories(path, mask).ToList();
				else
					return Directory.GetFiles(path, mask).ToList();
			}
			var dupes = new HashSet<string>();
			foreach (var vmod in Directory.GetDirectories(Program.modbase).PlusOne(addy).PlusOne(LoadedAssetBundle.basePathCanon))
			{
				var mod = vmod + "/" + subpath;
				//Debug.Log($"[ABM] scan {mod}");
				if (!Directory.Exists(mod))
					continue;
				foreach (var f in isdirs?Directory.GetDirectories(mod, mask):Directory.GetFiles(mod, mask))
				{
					// build the new virtual path
					var np = f.Substring(vmod.Length + 1);
					if (!nobase)
						np = LoadedAssetBundle.basePathCanon + np;
					var npl = np.ToLower();
					if (dupes.Contains(npl)) continue;
					dupes.Add(npl);
					Debug.Log($"[CACHE] Found {np} ({npl}) isdir={isdirs}");
					res.Add(np);
				}
			}
			return dirCache[key] = res;
		}
#endif


		// Canonizes path
		public static Dictionary<string, string> pathCache = new Dictionary<string, string>();
		public static string GetPath(string opath)
		{
#if USE_OLD_ABM
			return opath;
#else
			if (pathCache.TryGetValue(opath, out string cached))
				return cached;
			var path = Path.GetFullPath(opath);
			var subpath = ExtractABPath(path, out string addy);
			if (subpath == null)
				return path;
			var vpath = path;
			foreach (var mod in Directory.GetDirectories(Program.modbase).PlusOne(addy))
			{
				var nvpath = Path.Combine(mod, subpath);
				if (File.Exists(nvpath))
				{
					vpath = nvpath;
					break;
				}
			}
			if (!File.Exists(vpath) && !Directory.Exists(vpath))
				vpath = null;
			return pathCache[opath] = vpath;
#endif
		}
	}
}

public partial class GlobalMethod
{
	public static string[,] gArray;
	// XXX get rid of this horrible hack. Or at least add ability to concat files too.
	public static string LoadAllListText(string _assetbundleFolder, string _strLoadFile, List<string> _OmitFolderName = null)
	{
		if (_strLoadFile.IsNullOrEmpty())
			return null;
		if (_strLoadFile.StartsWith("dan_kh"))
		{
			Trace.Back("HDAN!");
		}
		if (Program.settings.fetchAssets)
		{
			if (CSV.LoadLst(_assetbundleFolder, _strLoadFile, out gArray))
			{
				Debug.Log($"[CACHE] Loading multi-LST {_assetbundleFolder}/{_strLoadFile}");
				return "@garray";
			}
		}
		var res = _LoadAllListText(_assetbundleFolder, _strLoadFile, _OmitFolderName);
		if (res == "" || res == null || !Patchwork.Program.settings.dumpAssets || File.Exists(Cache.ABPath(_assetbundleFolder, _strLoadFile, "csv")))
			return res;
		Debug.Log($"[CACHE] Saving multi-LST {_assetbundleFolder}/{_strLoadFile}");
		var ex = ScriptableObject.CreateInstance<ExcelData>();
		ex.Import(CSV.ParseTSV(res));
		Cache.Save(ex, _assetbundleFolder, _strLoadFile);
		return res;
	}
}

public class SpriteCache<T>
{
	public Dictionary<T, Sprite> cache = new Dictionary<T, Sprite>();
	public bool Get(T key, string bundle, string name, out Sprite ret)
	{
#if !USE_OLD_ABM
		ret = null;
		if (!Program.settings.cacheSprites || !cache.TryGetValue(key, out ret))
		{
			Debug.Log($"[SPRITE] Miss {bundle}/{name} @ {key}");
			// dont cache the sprite texture when the sprite is cached as such
			var tex = CommonLib.LoadAsset<Texture2D>(bundle, name);
			if (tex == null)
			{
				Debug.Log("Failed to load sprite");
				return false;
			}
			ret = Sprite.Create(tex, new Rect(0f, 0f, (float)tex.width, (float)tex.height), new Vector2(0.5f, 0.5f));
			if (Program.settings.cacheSprites)
			{
				cache[key] = ret;
				UnityEngine.Object.DontDestroyOnLoad(ret);
			}
		}
		else
		{
			Debug.Log($"[SPRITE] Hit {bundle}/{name} @ {key}");
		}
		//ret = UnityEngine.Object.Instantiate(ret);
		return true;
#else
		Texture2D tex = CommonLib.LoadAsset<Texture2D>(bundle, name, false);
		ret = null;
		if (tex != null)
		{
			ret = Sprite.Create(tex, new Rect(0f, 0f, (float)tex.width, (float)tex.height), new Vector2(0.5f, 0.5f));
		}
		return tex != null;
#endif
	}
}
