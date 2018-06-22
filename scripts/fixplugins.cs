// more or less ad-hoc script to load bepinex and ipa plugins

using System;
using System.IO;
using System.Reflection;
using UnityEngine;
using Patchwork;
using System.Linq;
using System.Collections.Generic;
using MessagePack;
using static ChaListDefine;
using System.Reflection.Emit;
using IllusionPlugin;
using UnityEngine.SceneManagement;

public class fixplugins : MonoBehaviour
{
	public static HashSet<string> blacklist = new HashSet<string>()
	{
		// if we load plugins on our own, these assemblies won't work
		"extensiblesaveformat",
		"resourceredirector",
		"sideloader",
		"demosaic",
		"zextensiblesaveremover",
		"sliderunlocker",
		"texrespatch",
		"ipaloader",
		"illusionplugin",
	};
	public static Assembly shimDll;
	public static GameObject bgo => GameObject.Find("BepInEx_Manager");
	public static bool hasBepinGo => bgo != null;
	public static bool hasBepinAss =>
		AppDomain.CurrentDomain.GetAssemblies().First(x => x.FullName.ToLower().StartsWith("bepinex")) != null;
	
	public Assembly AssResolver(object sender, ResolveEventArgs args)
	{
		var ln = args.Name.ToLower();
		if (ln.StartsWith("bepinex"))
			return shimDll;
		foreach (var n in blacklist)
			if (ln.StartsWith(n))
				return Assembly.GetExecutingAssembly();
		return null;
	}
	void OnDestroy()
	{
		AppDomain.CurrentDomain.AssemblyResolve -= AssResolver;
	}

	// bepinex can crash with us for multitude reasons. try to resuscite it, or
	// at least the plugins.
	void Start()
	{
		// its already in appdomain, it probably loaded correctly
		if (hasBepinGo)
		{
			print("BepinEx already loaded and (probably) running ok.");
			fixFilters();
			return;
		}
		shimDll = Assembly.Load("shims");
		if (shimDll == null)
		{
			print("Can't load shim dll");
			return;
		}
		// it broke, so load all plugins "manually"

		if (Script.getset("fixplugins", true))
			return;
		AppDomain.CurrentDomain.AssemblyResolve += AssResolver;
		//ScriptEnv.OnPrint += (msg) => BepInEx.BepInLogger.EntryLogger(msg, true);
		string root = Path.GetFullPath(Application.dataPath + "/../");
		bepath = root + "bepinex";
		if (!(YoLoad(bepath + "/0Harmony.dll") || YoLoad(bepath + "/core/0Harmony.dll") || YoLoad(Application.dataPath + "/Managed/0Harmony.dll")))
			return;
		Load(new string[] { bepath, bepath + "/ipa", /* root + "/plugins"*/ });
	}

	public static string bepath;

	public bool YoLoad(string path)
	{
		try
		{
			Assembly.Load(AssemblyName.GetAssemblyName(path));
			return true;
		}
		catch {
			return false;
		};
	}

	public void Load(string[] path)
	{
		Script.Evaluator.pause = true;
		foreach (var dll in Ext.GetFilesMulti(path, "*.dll"))
		{
			var pn = Path.GetFileNameWithoutExtension(dll);
			try
			{
				var an = AssemblyName.GetAssemblyName(dll);
				if (IsBlacklisted(an.Name))
					continue;
				var ass = Assembly.Load(an);
				var hasgo = false;
				var types = ass.GetTypesSafe();
				foreach (var t in types)
				{
					if (t.BaseType?.Name == "BaseUnityPlugin" && CheckFilter(t))
					{
						gameObject.AddComponent(t);
						hasgo = true;
					}
				}
				if (!hasgo)
				{
					foreach (var t in types)
					{
						if (!typeof(IPlugin).IsAssignableFrom(t))
							continue;
						var ip = Activator.CreateInstance(t) as IPlugin;
						ipa.Add(ip);
						ip.OnApplicationStart();
						hasgo = true;
					}
				}
				if (hasgo)
					print($"Loaded {pn}");
			}
			catch (Exception ex)
			{
				print($"Failed to load {pn}: {ex.ToString()}");
			};
		}
		Script.Evaluator.pause = false;
		foreach (var ip in ipa)
			ip.OnLevelWasLoaded(SceneManager.GetActiveScene().buildIndex);

	}

	// ipa proxies
	public bool first;
	public void Update()
	{
		if (!first)
		{
			first = true;
			foreach (var ip in ipa)
				ip.OnLevelWasInitialized(SceneManager.GetActiveScene().buildIndex);
		}
		foreach (var ip in ipa)
			ip.OnUpdate();
	}

	public void OnLateUpdate()
	{
		foreach (var ip in ipa)
			(ip as IEnhancedPlugin)?.OnLateUpdate();
	}

	public void OnFixedUpdate()
	{
		foreach (var ip in ipa)
			ip.OnFixedUpdate();
	}
	void OnApplicationQuit()
	{
		foreach (var ip in ipa)
			ip.OnApplicationQuit();
	}

	public List<IPlugin> ipa = new List<IPlugin>();

	public bool IsBlacklisted(string fn)
	{
		fn = Path.GetFileNameWithoutExtension(fn).ToLower();
		return blacklist.Contains(fn);
	}

	public bool CheckFilter(Type t)
	{
		// already running
		if (gameObject.GetComponent(t) != null)
			return false;
		// no filter
		var attr = t.GetCustomAttributes(true).FirstOrDefault(x => x.GetType().Name == "BepInProcess");
		if (attr == null)
		{
			return true;
		}
		var filter = attr.GetType().GetProperty("ProcessName").GetValue(attr) as string;
		if (filter == null)
			return true;
		if (Program.isStudio && filter.ToLower().StartsWith("charastudio"))
			return true;
		if (!Program.isStudio && filter.ToLower().StartsWith("koikatu"))
			return true;
		return false;
	}
	public void fixFilters()
	{
		foreach (var ass in AppDomain.CurrentDomain.GetAssemblies())
		{
			if (ass is AssemblyBuilder)
				continue;
			foreach (var t in ass.GetTypesSafe())
				if (t.BaseType?.Name == "BaseUnityPlugin")
				{
					try
					{
						if (CheckFilter(t))
						{
							print("Instancing filtered plugin " + t.Name);
							gameObject.AddComponent(t);
						}
					}
					catch { };
				}
		}
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

