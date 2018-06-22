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

namespace Patchwork
{
	[System.AttributeUsage(System.AttributeTargets.All)]
	public class Prio : System.Attribute
	{
		public int prio;
		public Prio(int n)
		{
			prio = n;
		}
	}
	

	/*
	[System.AttributeUsage(System.AttributeTargets.Event)]
	public class ScriptEventx : System.Attribute
	{
		[ScriptEvent]
		public static event Func<string, string, bool> OnScene;
		public static bool Scene(string name, string name2)
		{
			if (OnScene != null)
			{
				foreach (var cb in OnScene.GetInvocationList())
					if ((bool)cb.DynamicInvoke(new object[] { name, name2 }))
						return true;
			}
			return false;
		}
		public static Dictionary<string, KeyValuePair<EventInfo, List<KeyValuePair<int, System.Delegate>>>> events = new Dictionary<string, KeyValuePair<EventInfo, List<KeyValuePair<int, System.Delegate>>>>();
		public static void Init()
		{
			foreach (var t in Assembly.GetExecutingAssembly().GetExportedTypes())
			{
				foreach (var ev in t.GetEvents())
				{
					var attr = ev.GetCustomAttributes(typeof(ScriptEvent), true);
					if (attr != null && attr.Length > 0)
						events[ev.Name] = new KeyValuePair<EventInfo, List<KeyValuePair<int, System.Delegate>>>(ev, new List<KeyValuePair<int, System.Delegate>>());
				}
			}
		}

		public static void Reset()
		{
			// Nuke all event handlers
			foreach (var kv in events)
			{
				var ei = kv.Value;
				ei.Key.DeclaringType.GetField(kv.Key, BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, null);
				ei.Value.Clear();
			}
		}
		
		public static void Register(MonoBehaviour mb)
		{
			var mbt = mb.GetType();
			foreach (var m in mbt.GetMethods())
			{
				int prio = 0;
				string mname = m.Name;
				try
				{
					var lp = mname.Split('_').Last();
					prio = int.Parse(lp);
					mname = mname.Substring(0, mname.Length - lp.Length - 1);
				}
				catch { };
				
				if (events.ContainsKey(mname))
				{
					var evi = events[mname];
					System.Delegate dele = null;
					try
					{
						dele = System.Delegate.CreateDelegate(evi.Key.EventHandlerType, mb, m);
					}
					catch (Exception ex)
					{
						Script.print($"Couldn't wire method {mbt.Name}.{m} to event {evi.Key.DeclaringType.Name}.{evi.Key}: {ex.Message}");
					}

					var mipair = new KeyValuePair<int, System.Delegate>(prio, dele);
					evi.Value.Add(mipair);
				}
			}
		}
		public static void Wire()
		{
			foreach (var kv in events)
			{
				var ei = kv.Value;
				// sort by prio
				ei.Value.Sort((b, a) => { return a.Key - b.Key; });
				foreach (var hand in ei.Value)
				{
					//Script.print($"Register {hand.Value} prio {hand.Key}");
					try
					{
						ei.Key.AddEventHandler(null, hand.Value);
					} catch (Exception ex) {
						Script.print(ex);
					}
				}
			}
		}
	}*/

	public partial class Script : InteractiveBase
	{
		public static ScriptEvents On;
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

		public static Dictionary<string, Type> Components = new Dictionary<string, Type>();
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
		public static List<string> scriptFiles = new List<string>();
		public static bool reload(Action destroyer = null)
		{
			if (Evaluator != null)
				Evaluator.pause = true;
			print("Trying to (re)load script env.");
			var scripts = Path.Combine(UserData.Path, "scripts");
			try { Directory.CreateDirectory(scripts); } catch { };
			scriptFiles.Clear();
			foreach (var f in Directory.GetFiles(scripts, "*.*"))
				if (f.EndsWith(".cs"))
					scriptFiles.Add(f);
				else if (f.EndsWith(".dll"))
				{
					try
					{
						Assembly.Load(AssemblyName.GetAssemblyName(f));
						scriptFiles.Add(f);
					} catch (Exception ex)
					{
						print($"WARNING: Failed to load {f} => {ex.Message}");
					}
				}
			var neweva = MonoScript.New(new Reporter(), typeof(Script));
			var newasm = neweva.LoadScripts(scriptFiles, Program.tempbase);
			if (newasm == null)
			{
				print("Scripts reload failed.");
				if (Evaluator != null)
					Evaluator.pause = false;
				return false;
			}
			neweva.ReferenceAssembly(newasm);
			var init = neweva.InteractiveBaseClass.GetMethod("EnvInit");
			if (destroyer != null)
				destroyer();
			if (Evaluator != null)
				Evaluator.Dispose();
			Evaluator = neweva;
			Output = Evaluator.tw;
			Error = Evaluator.tw;
			if (init != null)
				init.Invoke(null, new object[] { });
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

