// Compatibility for plugins with sheer brute force. No regrets.

using System;
using System.IO;
using System.Reflection;
using UnityEngine;
using Patchwork;
using System.Linq;
using System.Collections.Generic;

public class fixplugins : MonoBehaviour
{
	public static bool hasBepinGo => GameObject.Find("BepInEx_Manager") != null;
	public static bool hasBepinAss =>
		AppDomain.CurrentDomain.GetAssemblies().First(x => x.FullName.ToLower().StartsWith("bepinex")) != null;

	void Awake()
	{
		// its already in appdomain, it probably loaded correctly
		if (hasBepinGo)
		{
			print("BepinEx already loaded and (probably) running ok.");
			fixFilters();
			return;
		}
		print("Trying to fix plugins");
		// it broke, so load all plugins "manually"
		string path = Path.GetFullPath(Application.dataPath + "/../bepinex");
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
		catch {};

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
				foreach (var t in ass.GetTypesSafe())
					if (t.BaseType?.Name == "BaseUnityPlugin")
						gameObject.AddComponent(t);
			}
			catch (Exception ex)
			{
				print(ex);
			};
		}
	}

	public void fixFilters()
	{
		foreach (var ass in AppDomain.CurrentDomain.GetAssemblies())
			foreach (var t in ass.GetTypesSafe())
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

	public void OnCardLoad(ChaFile f, BlockHeader bh)
	{
	}
	public void OnCardSave(ChaFile f, List<object> blocks)
	{
	}
}

