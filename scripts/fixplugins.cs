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
using System.Diagnostics;

public class FixPlugins : ScriptEvents
{

	public class FakeProcess : EzHook<FakeProcess>
	{
		public static string ProcessName => isStudio ? "CharaStudio" : "Koikatu";
	}

	public override void Awake()
	{
		FakeProcess.ApplyTo(typeof(Process));
		var bepin = Assembly.Load("BepInEx");
		var self = Script.baseAssembly;
		if (bepin == null || bepin == self)
		{
			if (bepin != null)
				print("No working bepinex found; using shim");
			return;
		}
		EzHook.Apply(bepin.GetType("BepInEx.Paths"), self.GetType("BepInEx.Paths"));
		try
		{
			var flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
			bepin.GetType("BepInEx.Logger").GetMethod("SetLogger", flags).Invoke(null, new object[] {
				Activator.CreateInstance(bepin.GetType("BepInEx.Logging.UnityLogWriter")) });
		}
		catch (Exception ex) {
			print(ex);
		};
		print("Using " + bepin.FullName);
	}
}