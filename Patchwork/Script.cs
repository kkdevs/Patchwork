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

namespace Patchwork
{
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

		/// <summary>
		/// Attach MBProxy to a singleton GameObject
		/// </summary>
		public static GameObject go;
		public static void Attach()
		{
			if (go != null) return;
			// And wire the dispatcher to monob proxy
			go = new GameObject("Scripts");
			go.AddComponent<MBProxy>();
			DontDestroyOnLoad(go);
		}

		public void OnApplicationPause(bool pauseStatus) { Script.On.OnApplicationPause(pauseStatus); }
		public void OnApplicationFocus(bool focus) { Script.On.OnApplicationFocus(focus); }
		public void OnLevelWasLoaded(int level) { Script.On.OnLevelWasLoaded(level); }
		public void OnApplicationQuit()
		{
			Script.On.OnApplicationQuit();
			foreach (var ip in ipa)
				ip.OnApplicationQuit();
		}
		public void Awake() { Script.On.Awake(); }
		public bool first;
		public void Update() {
			Script.On.Update();
			if (!first)
			{
				// shortcutskoi workaround
				if (Camera.main == null)
					return;
				first = true;
				foreach (var ip in ipa)
				{
					ip.OnApplicationStart();
					ip.OnLevelWasInitialized(SceneManager.GetActiveScene().buildIndex);
				}
			}
			foreach (var ip in ipa)
				ip.OnUpdate();
		}
		public void LateUpdate() {
			Script.On.LateUpdate();
			foreach (var ip in ipa)
				(ip as IEnhancedPlugin)?.OnLateUpdate();
		}
		public int tick;
		public void FixedUpdate() {
			if (tick-- < 0)
			{
				tick = 60;
				Script.On.Occasion();
			}
			
			Script.On.FixedUpdate();
			foreach (var ip in ipa)
				ip.OnFixedUpdate();
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
		public bool enabled;
		public List<Type> entrypoint = new List<Type>();

		public static List<ScriptEntry> list = new List<ScriptEntry>();

		/// <summary>
		/// Add a script entry to the list
		/// </summary>
		public void Add()
		{
			if (source.EndsWith(".dll"))
			{
				try
				{
					Add(Ext.LoadAssembly(source));
				}
				catch (Exception ex)
				{
					Trace.Info(ex.ToString());
				}
			} else if (source.EndsWith(".cs"))
			{
				Add(Ext.LoadTextFile(source));
			}
		}

		/// <summary>
		/// Add script entries contained in a loaded DLL
		/// </summary>
		/// <param name="ass"></param>
		public void Add(Assembly ass)
		{
			foreach (var t in ass.GetTypesSafe())
				if (typeof(IllusionPlugin.IPlugin).IsAssignableFrom(t) || typeof(BepInEx.BaseUnityPlugin).IsAssignableFrom(t))
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
				if (tag[0] == "//@INFO:") info = rest;
				if (tag[0] == "//@VER:") version = rest;
				if (tag[0] == "//@DEP:") deps = rest.ToLower().Split(' ').ToList();
			}
			list.Add(this);
		}

		public static IEnumerable<object> GetSources()
		{
			foreach (var script in list)
				if (script.enabled && script.ass == null)
					yield return script.source;
		}

		
		/// <summary>
		/// (re)Run the scripts collected thus far
		/// </summary>
		public static Assembly CompileScripts()
		{
			// compile
			var scriptass = Script.Evaluator.StaticCompile(GetSources());
			Dictionary<string, List<MethodInfo>> broadcast = new Dictionary<string, List<MethodInfo>>();

			// populate the base dispatch list
			foreach (var t in typeof(ScriptEvents).GetMethods(BindingFlags.Public | BindingFlags.Instance))
				if (!broadcast.ContainsKey(t.Name))
					broadcast[t.Name] = new List<MethodInfo>();

			var dispatcher = new StringBuilder();
			dispatcher.AppendLine("using System.Collections.Generic;");
			dispatcher.AppendLine("public class ScriptDispatcher : Patchwork.ScriptEvents {");

			// collect the methods we'll broadcast to
			foreach (var t in scriptass.GetTypesSafe())
			{
				if (!typeof(ScriptEvents).IsAssignableFrom(t))
					continue;
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
				dispatcher.AppendLine("override " + m.GetSignature());
				dispatcher.AppendLine("{");
				// early exit?
				// TODO: support enumerators
				var isexit = m.ReturnType == typeof(bool);
				if (isexit)
					dispatcher.Append("return ");
				// construct the call
				foreach (var fun in kv.Value)
				{
					dispatcher.Append(fun.DeclaringType.Name + "_instance." + fun.GetSignature(true));
					if (isexit && kv.Value.Last() != fun)
						dispatcher.Append("||");
					else
						dispatcher.AppendLine(";");
				}
				dispatcher.AppendLine("}");
			}
			dispatcher.AppendLine("}");
			var dstr = dispatcher.ToString();
			Debug.Log("Compiled dispatch: " + dstr);
			// compile this mess and instantiate the dispatcher
			var dispAss = Script.Evaluator.StaticCompile(new object[] { /*scriptass, */dstr.ToBytes() });
			Script.On?.OnDestroy();
			Script.On = Activator.CreateInstance(dispAss.GetTypesSafe().First(x => x.Name == "ScriptDispatcher")) as ScriptEvents;
			// if there is a GO already in place, we'll have to simulate start and awake
			if (MBProxy.go != null)
			{
				Script.On.Awake();
				Script.On.Start();
			}
			return scriptass;
		}

		/// <summary>
		/// Load dll scripts (once).
		/// </summary>
		public static void RunDLLs()
		{
			// Fire up the dlls
			foreach (var dll in list)
			{
				if (dll.ass == null) continue;
				foreach (var ep in dll.entrypoint)
				{
					try
					{
						if (typeof(IPlugin).IsAssignableFrom(ep))
						{
							var ip = Activator.CreateInstance(ep) as IPlugin;
							MBProxy.ipa.Add(ip);
						}
						else
							MBProxy.go.AddComponent(ep);
					}
					catch (Exception ex)
					{
						Script.print("{dll.name} failed to load: " + ex.Message);
					}
				}
			}
		}
	}


	/// <summary>
	/// Evaluator base class ("local variables" appear in it), compiler and overall script environment.
	/// </summary>
	public partial class Script : InteractiveBase
	{
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
			Evaluator = MonoScript.New(new Reporter(), typeof(Script), Program.tempbase);
			Output = Evaluator.tw;
			Error = Evaluator.tw;
			var sass = ScriptEntry.CompileScripts();
			var newbase = sass.GetType("ScriptEnv");
			if (newbase != null)
				Evaluator.InteractiveBaseClass = newbase;
			if (!firstRun)
			{
				firstRun = true;
				MBProxy.Attach();
				ScriptEntry.RunDLLs();
			}
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
}

