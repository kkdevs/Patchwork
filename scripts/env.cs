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
		foreach (var src in Script.scriptFiles)
			tss[src] = File.GetLastWriteTime(src);
		Application.logMessageReceived += forward;
	}
	void forward(string logString, string stackTrace, LogType type)
	{
		var frames = (new StackTrace()).GetFrames();
		string caller = $"{type.ToString()}";
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

	public void OnDestroy()
	{
		Application.logMessageReceived -= forward;
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
					Object.DestroyImmediate(ScriptEnv.G);
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
		G = new GameObject("Script Environment");
		Object.DontDestroyOnLoad(G);
		var nadd = 0;
		foreach (var t in Assembly.GetExecutingAssembly().GetTypes()) {
			if (t.BaseType == typeof(MonoBehaviour)) {
				G.AddComponent(t);
				nadd++;
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
