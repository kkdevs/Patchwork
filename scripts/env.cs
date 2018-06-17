using Patchwork;
using System.Collections.Generic;
using System.Reflection;
using System.IO;
using UnityEngine;
using System.Diagnostics;


public class Reloader : MonoBehaviour
{
	int ticks;
	bool stop;
	Dictionary<string, System.DateTime> tss = new Dictionary<string, System.DateTime>();
	public void Awake()
	{
		print("Initializing reloader from " + Assembly.GetExecutingAssembly().FullName);
		foreach (var src in Script.scriptFiles)
			tss[src] = File.GetLastWriteTime(src);
		Application.logMessageReceived += forward;
	}
	public void OnDestroy()
	{
		Application.logMessageReceived -= forward;
	}

	public static List<string> logFilter = new List<string>();
	void forward(string logString, string stackTrace, LogType type)
	{
		if (logString.Contains("AssetBundle with the same files is already loaded."))
			return;
		string caller = $"{type.ToString()}";
		if (logFilter.Contains(caller))
			return;
		var frames = (new StackTrace()).GetFrames();
		for (int i = 0; i < frames.Length; i++)
		{
			if (frames[i].GetMethod().Name == "print")
			{
				var cal = frames[i + 1].GetMethod();
				caller = $"{cal.DeclaringType.Name}.{cal.Name}";
			}
		}
		Script.print($"[{caller}] {logString}");
	}

	public void Update()
	{
		if (++ticks < 60) return;
		ticks = 0;
		if (stop) return;
		bool fail = false;
		foreach (var src in Script.scriptFiles.ToArray())
		{
			var cts = File.GetLastWriteTime(src);
			if (!fail && (tss[src] != cts))
			{
				fail = fail || !Script.reload(() =>
				{
					stop = true;
					print("Destroying scriptenv GO");
					Object.DestroyImmediate(ScriptEnv.G);
					ScriptEnv.G = null;
				});
				if (stop)
					break;
			}
			tss[src] = cts;
		}
	}
}

public partial class ScriptEnv : Script
{
	public static GameObject G;
	public static void EnvInit()
	{
		G = new GameObject("ScriptEnv");
		Object.DontDestroyOnLoad(G);
		var nadd = 0;
		foreach (var t in Assembly.GetExecutingAssembly().GetTypes()) {
			if (t.BaseType == typeof(MonoBehaviour))
			{
				G.AddComponent(t);
				nadd++;
			}
			else
			// A dynamic component.
			if (typeof(Component).IsAssignableFrom(t) && t.BaseType.Assembly.GetName().Name == "Assembly-CSharp" && t.BaseType.Name != "AutoRun")
			{
				System.Type ot = null;
				try
				{
					if (Components.TryGetValue(t.BaseType.Name, out ot))
						foreach (var v in Object.FindObjectsOfType(ot))
							if (v.GetType() == ot)
								Object.DestroyImmediate(v);
				}
				catch (System.Exception ex) { print(ex.ToString()); }
				if (ot != null && ot.Name != t.Name)
					print($"WARNING: {ot.Name} overriden by {t.Name}.");
				Components[t.BaseType.Name] = t;
				print($"Registered component {t.Name} => {t.BaseType.Name}");
			}
		}
		eval("using System.Linq; using System.Collections.Generic; using System.Collections; using Patchwork; using UnityEngine; using UnityEngine.SceneManagement;");
		print($"Script environment initialized, {nadd} MonoBs running.");
	}
	public static object clear
	{
		get
		{
			Program.form.replOutput.Text = "";
			return typeof(Sentinel);
		}
	}
}
