using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;
using Mono.CSharp;
using System.CodeDom.Compiler;
using System.Reflection.Emit;
using System.Windows;
using UnityEngine;
using System;

using Object = UnityEngine.Object;
using Forms = System.Windows.Forms;
using System.Collections;
using System.Windows.Forms;
using UnityEngine.SceneManagement;
using IllusionPlugin;

/// <summary>
/// Set scriptevent priority
/// </summary>
[System.AttributeUsage(System.AttributeTargets.All)]
public class Prio : System.Attribute
{
	public int prio;
	public Prio(int n)
	{
		prio = n;
	}
}


/// <summary>
/// Route global MB events to scriptevents. Also piggyback ipa events.
/// </summary>
public class MBProxy : MonoBehaviour
{
	public static List<IPlugin> ipa = new List<IPlugin>();

	public static List<Action> pendingActions = new List<Action>();

	/// <summary>
	/// Attach MBProxy to a singleton GameObject
	/// </summary>
	/// 
	public static GameObject go;
	public static MBProxy instance;
	public static bool hasScene;
	public static void Attach()
	{
		if (go != null) return;
		// And wire the dispatcher to monob proxy
		go = new GameObject("Scripts");
		instance = go.AddComponent<MBProxy>();
		DontDestroyOnLoad(go);
		SceneManager.sceneLoaded += (scene, mode) =>
		{
			Debug.Log($"[SCENE] change to {scene.name} {scene.buildIndex}");
			Script.On.OnLevelWasLoaded(scene, mode);
			foreach (var ip in ipa)
			{
				try
				{
					Debug.Log($"Notifying {ip.GetType().Name}");
					ip.OnLevelWasLoaded(scene.buildIndex);
				}
				catch (Exception ex)
				{
					print(ex);
				};
				hasScene = true;
			}
		};
	}

	public void OnApplicationPause(bool pauseStatus) { Script.On.OnApplicationPause(pauseStatus); }
	public void OnApplicationFocus(bool focus) { Script.On.OnApplicationFocus(focus); }
	public void OnApplicationQuit()
	{
		Script.On.OnApplicationQuit();
		foreach (var ip in ipa)
		{
			try
			{
				ip.OnApplicationQuit();
			}
			catch (Exception ex)
			{
				print(ex);
			};
		}
	}
	public void Awake() {
		instance = this;
		Script.On.Awake();
	}
	public bool first;
	public void Update() {
		Script.On.Update();
		try
		{
			foreach (var ip in ipa)
				ip.OnUpdate();
		} catch { };
		if (hasScene)
		{
			foreach (var ip in ipa)
			{
				try
				{
					ip.OnLevelWasInitialized(SceneManager.GetActiveScene().buildIndex);
				}
				catch { };
			}
			hasScene = false;
		}
	}
	public void LateUpdate() {
		Script.On.LateUpdate();
		try
		{
			foreach (var ip in ipa)
				(ip as IEnhancedPlugin)?.OnLateUpdate();
		}
		catch { };
	}
	public int tick;
	public void FixedUpdate() {
		if (tick-- < 0)
		{
			tick = 60;
			Script.On.Occasion();
			Patchwork.FixWindow();
		}
			
		Script.On.FixedUpdate();
		try
		{
			foreach (var ip in ipa)
				ip.OnFixedUpdate();
		}
		catch { };

		foreach (var cb in pendingActions)
			cb();
		pendingActions.Clear();
	}
	public void OnDestroy() { Script.On.OnDestroy(); }
	public void OnDisable() { Script.On.OnDisable(); }
	public void OnEnable() { Script.On.OnEnable(); }
	public void OnGUI() { Script.On.OnGUI(); }
	public void Start() { Script.On.Start(); }
}




/// <summary>
/// Represents one script entry as well as the collection of enabled scripts
/// </summary>
public class ScriptEntry
{
	public string source; // source file of this script, dll or .cs
	public string name;
	public string version = "";
	public string info = "";
	public Assembly ass;
	public List<string> deps = new List<string>();
	public ListViewItem listView;

	public List<Type> entrypoint = new List<Type>();

	public static List<ScriptEntry> list = new List<ScriptEntry>();

	public static bool reloadPending;
	public bool _enabled;
	public bool enabled
	{
		get
		{
			return _enabled;
		}
		set
		{
			if (!Patchwork.launched)
			{
				_enabled = value;
				return;
			}
				
			if (ass != null)
			{
				// DLL can't be disabled
				if (value == false)
				{
					MBProxy.pendingActions.Add(() =>
					{
						listView.Checked = true;
					});
					return;
				}
				_enabled = value;
				RunDLL();
				return;
			}

			_enabled = value;
			// Otherwise a script, just recompile everything
			if (!reloadPending)
			{
				reloadPending = true;
				MBProxy.pendingActions.Add(() =>
				{
					Script.reload();
					reloadPending = false;
				});
			}
		}
	}

	/// <summary>
	/// Add a script entry to the list
	/// </summary>
	public bool Add()
	{
		if (source.EndsWith(".dll"))
		{
			try
			{
				Add(Ext.LoadAssembly(source));
				return true;
			}
			catch (Exception ex)
			{
				Debug.Info("Assembly add failed: " + ex.ToString());
			}
		} else if (source.EndsWith(".cs"))
		{
			Add(Ext.LoadTextFile(source));
			return true;
		}
		return false;
	}

	/// <summary>
	/// Add script entries contained in a loaded DLL
	/// </summary>
	/// <param name="ass"></param>
	public void Add(Assembly ass)
	{
		foreach (var t in ass.GetTypesSafe())
			if (typeof(IPlugin).IsAssignableFrom(t) || (t.BaseType != null && t.BaseType.Name == "BaseUnityPlugin"))
				entrypoint.Add(t);
		if (entrypoint.Count == 0)
			return;
		this.ass = ass;
		name = ass.GetName().Name;
		version = ass.GetName().Version.ToString().Trim();
		info = ass.GetCustomAttributes(typeof(AssemblyDescriptionAttribute), false).OfType<AssemblyDescriptionAttribute>().FirstOrDefault()?.Description ?? "";
		list.Add(this);
	}

	/// <summary>
	/// Add script as a source file
	/// </summary>
	/// <param name="body">snapshot of script body to pull metadata from</param>
	public void Add(string body)
	{
		if (body == null) return;
		var lines = body.Split('\n');
		int probe = 10;
		foreach (var l in body.Split('\n'))
		{
			if (probe-- < 0) break;
			var tag = l.Split(' ');
			var rest = l.Substring(tag[0].Length).Trim();
			var t = tag[0].Trim();
			if (t == "//@INFO:") info = rest;
			if (t == "//@VER:") version = rest;
			if (t == "//@DEP:") deps = rest.ToLower().Split(' ').ToList();
			if (t == "//@OFF")
			{
				enabled = false; // must be manually enabled
				if (!Patchwork.settings.scriptDisabled.Contains(name.ToLower()))
					Patchwork.settings.scriptDisabled.Add(name.ToLower());
			}
		}
		list.Add(this);
	}

	public static IEnumerable<object> GetSources()
	{
		foreach (var script in list)
			if (script.enabled && script.ass == null)
				yield return script.source;
	}


	public static Assembly oldScripts;
	/// <summary>
	/// (re)Run the scripts collected thus far
	/// </summary>
	public static Assembly CompileScripts()
	{
		// compile
		MonoScript.Unload(oldScripts);
		var scriptass = Script.Evaluator.StaticCompile(GetSources(), "s_");
		oldScripts = scriptass;
		Dictionary<string, List<MethodInfo>> broadcast = new Dictionary<string, List<MethodInfo>>();

		// populate the base dispatch list
		foreach (var t in typeof(ScriptEvents).GetMethods(BindingFlags.Public | BindingFlags.Instance))
			if (!broadcast.ContainsKey(t.Name))
				broadcast[t.Name] = new List<MethodInfo>();

		var dispatcher = new StringBuilder();
		dispatcher.AppendLine("using System.Collections.Generic;");
		dispatcher.AppendLine("public class ScriptDispatcher : ScriptEvents {");

		// collect the methods we'll broadcast to
		foreach (var t in scriptass.GetTypesSafe())
		{
			if (t.IsAbstract || t.IsInterface || (!t.IsPublic))
			{
				Debug.Log($"[SCRIPT] Skipping {t.Name} because it is abstract {t.IsAbstract} {t.IsInterface} {t.IsPublic} {t.BaseType?.Name ?? "no base"} {t?.BaseType?.BaseType?.Name ?? "no 2base"}");
				continue;
			}
			for (var bt = t.BaseType; bt != null && bt != typeof(Object); bt = bt.BaseType)
				if (bt.Name == "ScriptEvents")
					goto good;
			continue;
			good:
			foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.Instance))
			{
				if (!broadcast.ContainsKey(m.Name))
					continue;
				if (m.DeclaringType != t)
					continue;
				broadcast[m.Name].Add(m);
			}
			// populate the instances while at it.
			dispatcher.AppendLine($"public {t.Name} {t.Name}_instance = new {t.Name}();");
		}

		// sort em according to broadcast priority
		foreach (var kv in broadcast)
		{
			kv.Value.Sort((a, b) =>
			{
				var attra = new Prio(0);
				var attrb = new Prio(0);
				a.GetAttr(ref attra);
				b.GetAttr(ref attrb);
				return attrb.prio - attra.prio;
			});
		}

		// Now construct the dispatcher
		foreach (var kv in broadcast)
		{
			if (kv.Value.Count == 0) continue;
			var m = typeof(ScriptEvents).GetMethod(kv.Key);
			dispatcher.AppendLine("override " + m.GetSignature().Replace("&", "").Replace("+","."));
			dispatcher.AppendLine("{");
			// early exit?
			// TODO: support enumerators
			var isexit = m.ReturnType == typeof(bool);
			if (isexit)
				dispatcher.Append("return ");
			// construct the call
			foreach (var fun in kv.Value)
			{
				dispatcher.Append(fun.DeclaringType.Name + "_instance." + fun.GetSignature(true).Replace("&",""));
				if (isexit && kv.Value.Last() != fun)
					dispatcher.Append("||");
				else
					dispatcher.AppendLine(";");
			}
			dispatcher.AppendLine("}");
		}
		dispatcher.AppendLine("}");
		dispatcher.AppendLine("//" + scriptass.FullName);
		var dstr = dispatcher.ToString();
		Debug.Log("Compiled dispatch: " + dstr);
		// compile this mess and instantiate the dispatcher
		var dispAss = Script.Evaluator.StaticCompile(new object[] { /*scriptass, */dstr.ToBytes() }, "d_");
		Script.On?.OnDestroy();
		Script.On = Activator.CreateInstance(dispAss.GetTypesSafe().First(x => x.Name == "ScriptDispatcher")) as ScriptEvents;
		// if there is a GO already in place, we'll have to simulate start and awake
		if (MBProxy.go != null)
		{
			Script.On.Awake();
			Script.On.Start();
		}
		var newlist = new Dictionary<MonoBehaviour, ScriptEvents>();
		foreach (var kv in SingletonList.singletons)
		{
			if (kv.Value != Script.On)
				Script.On.OnSingleton(kv.Key, false);
			newlist[kv.Key] = Script.On;
		}
		SingletonList.singletons = newlist;
		MonoScript.Unload(dispAss);
		return scriptass;
	}

	/// <summary>
	/// Load dll scripts (once).
	/// </summary>
	public static void RunDLLs()
	{
		// Fire up the dlls
		foreach (var dll in list)
			dll.RunDLL();
	}

	public void RunDLL()
	{
		var dll = this;
		if (dll.ass == null) return;
		if (!dll.enabled) return;
		foreach (var ep in dll.entrypoint)
		{
			try
			{
				if (typeof(IPlugin).IsAssignableFrom(ep))
				{
					var ip = Activator.CreateInstance(ep) as IPlugin;
					MBProxy.ipa.Add(ip);
					ip.OnApplicationStart();
				}
				else
				{
					MBProxy.go.AddComponent(ep);
				}
				Script.print($"Loaded {dll.name}");
			}
			catch (Exception ex)
			{
				Script.print("{dll.name} failed to load: " + ex.Message);
			}
		}
	}
}


/// <summary>
/// Evaluator base class ("local variables" appear in it), compiler and overall script environment.
/// </summary>
public partial class Script : InteractiveBase
{
	public static Assembly baseAssembly => Assembly.GetExecutingAssembly();
	public static ScriptEvents On = new ScriptEvents();
	public static Dictionary<string, object> regDict = new Dictionary<string, object>();
	public static T registry<T>(string name) where T : class, new()
	{
		if (regDict.TryGetValue(name, out object obj))
			return obj as T;
		var v = new T();
		regDict[name] = v;
		return v;
	}
	public static T getset<T>(string name, T val) where T : struct
	{
		if (regDict.TryGetValue(name, out object obj))
		{
			regDict[name] = val;
			return (T)obj;
		}
		regDict[name] = val;
		return new T();
	}

	public class Reporter : TextWriter
	{
		public static Action<string> write;
		public override Encoding Encoding => Encoding.UTF8;
		public override void Write(char c)
		{
			write("" + c);
		}
		public override void Write(string s)
		{
			write(s);
		}
		public override void WriteLine(string s)
		{
			write(s);
			write("\n");
		}
	}
	public static new void print(object o)
	{
		write(o.ToString() + "\n");
	}
	public static void write(string s)
	{
		Reporter.write(s);
	}

	public static void pp(object o)
	{
		print(o);
	}

	public static object Invoke(string m, params object[] args)
	{
		var types = new Type[args.Length];
		for (int i = 0; i < args.Length; i++)
			types[i] = args[i]?.GetType() ?? typeof(object);
		var me = Evaluator.InteractiveBaseClass.GetMethod(m, BindingFlags.FlattenHierarchy | BindingFlags.Public | BindingFlags.Static, null, types, null);
		if (me == null)
		{
			print($"Failed to locate '{m}' in Script environment");
			return null;
		}
		return me.Invoke(null, args);
	}

	public static new MonoScript Evaluator;

	public static bool firstRun;
	public static bool reload()
	{
		var oldeva = Evaluator;
		Evaluator = MonoScript.New(new Reporter(), typeof(Script), (oldeva==null&&Patchwork.settings.cacheScripts)?Dir.cache:null);
		Output = Evaluator.tw;
		Error = Evaluator.tw;
		Assembly sass = null;
		try
		{
			sass = ScriptEntry.CompileScripts();
		} catch (Exception ex)
		{
			print(ex);
		}
		if (sass == null)
		{
			print("Script reload failed; trying to retain old script base.");
			Evaluator.Dispose();
			Evaluator = oldeva;
			return false;
		}
		oldeva?.Dispose();
		var newbase = sass.GetType("ScriptEnv");
		if (newbase != null)
			Evaluator.InteractiveBaseClass = newbase;
		if (!firstRun)
		{
			firstRun = true;
			MBProxy.Attach();
			ScriptEntry.RunDLLs();
		}
		print("Scripts reloaded");
		return true;
	}

	public static class Sentinel { }
	public static object eval(string str)
	{
		object ret = typeof(Sentinel);
		compile(str)?.Invoke(ref ret);
		return ret;
	}

	public static CompiledMethod compile(string str)
	{
		CompiledMethod compiled = null;
		Evaluator.Compile(str, out compiled);
		return compiled;
	}
}


