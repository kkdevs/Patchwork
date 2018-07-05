//@INFO: Various workarounds
//@VER: 2

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Patchwork;
using UnityEngine;
using UnityEngine.SceneManagement;

public class FixPlugins : ScriptEvents
{
	public override void Awake()
	{
		var bepin = Assembly.Load("BepInEx");
		if (bepin == null || bepin == Script.baseAssembly)
			return;
		print("Trying to revive bepin logger");
		try
		{
			var logger = bepin.GetType("BepInEx.Logger");
			var ulogger = bepin.GetType("BepInEx.Logging.UnityLogWriter");
			logger.GetMethod("SetLogger", BindingFlags.Static|BindingFlags.Public).Invoke(null, new object[] { Activator.CreateInstance(ulogger) });
		}
		catch (Exception ex)
		{
			print("Revival failed:" + ex.ToString());
			return;
		}
		print("Bepin logger revived");
	}
}