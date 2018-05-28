// Compatibility for Plugins, disregard any complaints
using System;
using System.IO;
using System.Reflection;
using UnityEngine;

public class fixplugins : MonoBehaviour
{
	public bool hasBepin()
	{
		foreach (var ass in AppDomain.CurrentDomain.GetAssemblies())
			if (ass.FullName.ToLower().StartsWith("bepinex"))
				return true;
		return false;
	}
	public fixplugins()
	{
		if (hasBepin())
			return;
		Patchwork.Trace.Log("Trying to fix plugins");
		// its already in appdomain, it probably loaded correctly
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
						var obj = gameObject.AddComponent(t);
					}
				}
			}
			catch (Exception ex)
			{
				Debug.Log(ex);
			};
		}
	}
}

