// Compatibility for plugins with sheer brute force. No regrets.
using System;
using System.IO;
using System.Reflection;
using UnityEngine;
using Patchwork;


public class fixplugins : Script.AutoRun
{
	public bool hasBepin()
	{
		foreach (var ass in AppDomain.CurrentDomain.GetAssemblies())
			if (ass.FullName.ToLower().StartsWith("bepinex"))
				return true;
		return false;
	}
	public void Awake()
	{
		// its already in appdomain, it probably loaded correctly
		if (hasBepin())
		{
			print("BepinEx already loaded and (probably) running ok.");
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
					AppDomain.CurrentDomain.Load(AssemblyName.GetAssemblyName(dll));
				}
				catch { };
			}
		}
		catch {};

		if (!hasBepin())
			try {
				AppDomain.CurrentDomain.Load(Application.dataPath + "/Managed/bepinex.dll");
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
				{
					if (t.BaseType?.Name == "BaseUnityPlugin")
					{
						gameObject.AddComponent(t);
					}
				}
			}
			catch (Exception ex)
			{
				print(ex);
			};
		}
	}
}

