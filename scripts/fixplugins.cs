//@INFO: Various workarounds
//@VER: 2

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using static Patchwork;
using UnityEngine;
using UnityEngine.SceneManagement;

public class FixPlugins : ScriptEvents
{
	public override void Awake()
	{
		var flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
		var exename = isStudio ? "CharaStudio.exe" : "Koikatu.exe";
		var bepin = Assembly.Load("BepInEx");
		if (bepin == null || bepin == Script.baseAssembly)
		{
			if (bepin != null)
				print("No working bepinex found; using shim");
			return;
		}
		print("Using " + bepin.FullName);
		try
		{
			var paths = bepin.GetType("BepInEx.Paths");
			paths.GetField("executablePath", flags).SetValue(null, Dir.root + exename);
			paths.GetProperty("PluginPath", flags).SetValue(null, Dir.root + "bepinex");
			paths.GetProperty("PatcherPluginPath", flags).SetValue(null, Dir.root + "bepinex/patchers");
			paths.GetProperty("BepInExAssemblyDirectory", flags).SetValue(null, Dir.root + "bepinex/core");
			paths.GetProperty("BepInExAssemblyPath", flags).SetValue(null, Dir.root + "bepinex/core/bepinex.dll");
		}
		catch (Exception ex)
		{
		}
		try
		{
			var logger = bepin.GetType("BepInEx.Logger");
			var ulogger = bepin.GetType("BepInEx.Logging.UnityLogWriter");
			logger.GetMethod("SetLogger", flags).Invoke(null, new object[] { Activator.CreateInstance(ulogger) });
		}
		catch (Exception ex)
		{
			print("Revival failed:" + ex.ToString());
			return;
		}
		print("Bepin logger revived");
	}
}