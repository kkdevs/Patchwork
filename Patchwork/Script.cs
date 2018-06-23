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

	public class ScriptEntry
	{
		public string name;
		public string source;
		public string version = "";
		public string info = "";
		public Assembly ass;
		public List<string> deps = new List<string>();
		public ListViewItem listView;
		public bool enabled;
		public List<Type> entrypoint = new List<Type>();
		public void SetAssembly(Assembly ass)
		{
			this.ass = ass;
			name = ass.GetName().Name;
			version = ass.GetName().Version.ToString().Trim();
			info = ass.GetCustomAttributes(typeof(AssemblyDescriptionAttribute), false).OfType<AssemblyDescriptionAttribute>().FirstOrDefault()?.Description ?? "";
		}
		public void SetScript(string body)
		{
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
		}
	}

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

