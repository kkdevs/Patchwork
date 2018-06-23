#if !USE_OLD_ABM
using Patchwork;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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

	public static string[] GetAllFileName(string assetBundleName, bool _WithExtension = true, string manifestAssetBundleName = null, bool isAllCheck = false)
	{
		return GetAllAssetName(assetBundleName, _WithExtension, manifestAssetBundleName);
	}
	private static bool CheckRegex(string _value, string _regex, RegexOptions _options)
	{
		Match match = Regex.Match(_value, _regex, _options);
		return match.Success;
	}

	public static string[] FindAllAssetName(string assetBundleName, string _regex, bool _WithExtension = true, RegexOptions _options = RegexOptions.None)
	{
		var ab = LoadedAssetBundle.Load(assetBundleName);
		if (ab == null) return null;
		_regex = _regex.ToLower();
		string[] result = (!_WithExtension) ? (from v in ab.GetAllAssetNames().Select(Path.GetFileNameWithoutExtension)
											   where CheckRegex(v, _regex, _options)
											   select v).ToArray() : (from v in ab.GetAllAssetNames().Select(Path.GetFileName)
																	  where CheckRegex(v, _regex, _options)
																	  select v).ToArray();
		return result;
	}
	public static string[] GetAllAssetName(string assetBundleName, bool _WithExtension = true, string manifestAssetBundleName = null, bool isAllCheck = false)
	{
		var ab = LoadedAssetBundle.Load(assetBundleName);
		if (ab == null) return null;
		return (!_WithExtension) ? ab.GetAllAssetNames().Select(Path.GetFileNameWithoutExtension).ToArray() : ab.GetAllAssetNames().Select(Path.GetFileName).ToArray();
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
	public class BundlePack
	{
		public AssetBundleManifest AssetBundleManifest;
	};
	public static Dictionary<string, BundlePack> ManifestBundlePack = new Dictionary<string, BundlePack>();
	public static bool isInitialized = false;
	public static string BaseDownloadingURL { get; set; }
	public static void Initialize(string basePath)
	{
		if (!isInitialized)
		{
			GameObject gameObject = new GameObject("AssetBundleManager", typeof(AssetBundleManager));
			UnityEngine.Object.DontDestroyOnLoad(gameObject);
			LoadedAssetBundle.Init(basePath);
			BaseDownloadingURL = basePath;
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
		return Cache.AssetABM(assetBundleName, assetName, type);
//		if (Cache.AssetABM(assetBundleName, assetName, type, manifestAssetBundleName, out AssetBundleLoadAssetOperation cached))
//			return cached;
//		return _LoadAsset(assetBundleName, assetName, type, manifestAssetBundleName);
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
		LoadAssetBundle(assetBundleName, false, null, levelName)?.Ensure();
		SceneManager.LoadScene(levelName, isAdditive ? LoadSceneMode.Additive : LoadSceneMode.Single);
		return new AssetBundleLoadLevelSimulationOperation();
	}

	public static AssetBundleLoadOperation LoadLevelAsync(string assetBundleName, string levelName, bool isAdditive, string manifestAssetBundleName = null)
	{
		LoadAssetBundle(assetBundleName, false, null, levelName)?.Ensure();
		var ll = new AssetBundleLoadLevelOperation(assetBundleName, levelName, isAdditive, manifestAssetBundleName);
		ll.Update();
		return ll;
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

#endif

namespace BepInEx
{
	public class BaseUnityPlugin : MonoBehaviour { }
	[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
	public class BepInPlugin : Attribute
	{
		public BepInPlugin(string GUID, string Name, string Version) { }
	}
	[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
	public class BepInDependency : Attribute
	{
		public enum DependencyFlags : int
		{
			HardDependency = 1,
			SoftDependency = 2,
		}
		public BepInDependency(string a, DependencyFlags b = DependencyFlags.HardDependency)
		{
		}
	}

	[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
	public class BepInProcess : Attribute
	{
		public string ProcessName { get; set; }
		public BepInProcess(string processName)
		{
			ProcessName = processName;
		}
	}
	namespace Logging
	{
		[Flags]
		public enum LogLevel
		{
			None = 0,
			Fatal = 1,
			Error = 2,
			Warning = 4,
			Message = 8,
			Info = 16,
			Debug = 32,
			All = Fatal | Error | Warning | Message | Info | Debug
		}
		public static class Logger
		{
			public static void Log(int level, object entry)
			{
				if ((level & 15) != 0)
					Trace.Log(entry.ToString());
				else
					Trace.Spam(entry.ToString());
			}

			public abstract class BaseLogger : TextWriter { };
			public static void SetLogger(BaseLogger logger)
			{
			}
		}
	}
	public static class BepInLogger
	{
		public static void Log(string entry, bool show = false)
		{
			if (show)
				Trace.Log(entry);
			else
				Trace.Spam(entry);

		}
		public static void Log(object entry, bool show, ConsoleColor color)
		{
			Log(entry.ToString(), show, color);
		}
		public static void Log(string entry, bool show, ConsoleColor color)
		{
			Log(entry, show);
		}
		public static void EntryLogger(string entry, bool show)
		{
			EntryLogged?.Invoke(entry, show);
		}
		public delegate void EntryLoggedEventHandler(string entry, bool show = false);
		public static event EntryLoggedEventHandler EntryLogged;
	}
	namespace Common
	{
		public static class Utility
		{
			public static string PluginsDirectory => Path.GetFullPath(Application.dataPath + "/../bepinex");
		}
	}

	public class ConfigWrapper<T>
	{
		public T Value { get; set; }
		public ConfigWrapper(string name, BaseUnityPlugin o, T val) { Value = val; }
		public event EventHandler SettingChanged;
		public void Clear() { }
	}
}

namespace IllusionPlugin
{
	public interface IPlugin
	{
		string Name { get; }
		string Version { get; }
		void OnApplicationQuit();
		void OnApplicationStart();
		void OnFixedUpdate();
		void OnLevelWasInitialized(int level);
		void OnLevelWasLoaded(int level);
		void OnUpdate();
	}
	public interface IEnhancedPlugin : IPlugin
	{
		string[] Filter { get; }
		void OnLateUpdate();
	}
}

