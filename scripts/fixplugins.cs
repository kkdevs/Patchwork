//@INFO: Compatibility for plugins

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

public class fixplugins : MonoBehaviour
{
	public static GameObject bgo => GameObject.Find("BepInEx_Manager");
	public static bool hasBepinGo => bgo != null;
	public static bool hasBepinAss =>
		AppDomain.CurrentDomain.GetAssemblies().First(x => x.FullName.ToLower().StartsWith("bepinex")) != null;
	
	// bepinex can crash with us for multitude reasons. try to resuscite it, or
	// at least the plugins.
	void Awake()
	{
		// its already in appdomain, it probably loaded correctly
		if (hasBepinGo)
		{
			print("BepinEx already loaded and (probably) running ok.");
			fixFilters();
			return;
		}

		if (Script.getset("fixplugins", true))
			return;

		print("Trying to fix plugins");
		// it broke, so load all plugins "manually"
		string path = Path.GetFullPath(Application.dataPath + "/../bepinex");
		for (int i = 0; i < 2; i++)
		{
			try
			{
				foreach (var dll in Directory.GetFiles(path + "/core", "*.dll"))
				{
					try
					{
						Assembly.Load(AssemblyName.GetAssemblyName(dll));
					}
					catch { };
				}
			}
			catch { };
		}

		if (!hasBepinAss)
			try {
				Assembly.Load(Application.dataPath + "/Managed/bepinex.dll");
			} catch {
				// none found, bail
				return;
			};

		foreach (var dll in Directory.GetFiles(path, "*.dll"))
		{
			try
			{
				var an = AssemblyName.GetAssemblyName(dll);
				var ass = AppDomain.CurrentDomain.Load(an);
				foreach (var t in ass.GetExportedTypes())
					if (t.BaseType?.Name == "BaseUnityPlugin")
						gameObject.AddComponent(t);
			}
			catch (Exception ex)
			{
				print("Plugin {path} may not work: {ex}");
			};
		}

	}

	// since we have different exe name, all filters based on that are
	// bogus.
	public void fixFilters()
	{
		foreach (var ass in AppDomain.CurrentDomain.GetAssemblies())
		{
			if (ass is AssemblyBuilder)
				continue;
			foreach (var t in ass.GetExportedTypes())
				if (t.BaseType?.Name == "BaseUnityPlugin")
				{
					try
					{
						if (t.GetCustomAttributes(false).First(x => x.GetType().Name == "BepInProcess") != null)
							if (gameObject.GetComponent(t) == null)
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

