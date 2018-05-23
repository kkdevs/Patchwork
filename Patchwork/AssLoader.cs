// Assloader does not work for "classic" IL monkey-patching mods, unless they
// meticuously respect OnDestroy() contract.
//
// All monobs contained within assplug are fired, then we periodically check if the file
// and version of the dll changed. If so, we tell the monobs to scram, and load new version
// of DLL, and fire it up again.
//
// No magic here.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

namespace Patchwork
{
	class AssLoader : MonoBehaviour
	{
		uint ts = 0;
		string basedir = Path.GetDirectoryName(Path.GetFullPath(Environment.GetCommandLineArgs()[0]));
		Dictionary<string, Assembly> knownDLLs = new Dictionary<string, Assembly>();
		Dictionary<string, List<Component>> monoBs = new Dictionary<string, List<Component>>();
		Dictionary<string, DateTime> timestamps = new Dictionary<string, DateTime>();
		List<string> loadPending = new List<string>();
		static GameObject pin;
		string[] dirs => Program.settings.libDirs.Split(new[] { '\r', '\n' });

		int throttle;
		public static void Init()
		{
			pin = new GameObject("ASS LOADER");
			if (pin == null)
			{
				Debug.Log("Failed to pin assloader, too early?");
			}
			UnityEngine.Object.DontDestroyOnLoad(pin);
			pin.AddComponent(typeof(AssLoader));
		}

		void Start()
		{
			AppDomain.CurrentDomain.AssemblyResolve += (s, args) =>
			{
				var shortname = new System.Reflection.AssemblyName(args.Name).Name;
				Debug.Log($"[ASSRESOLVE] Looking for shortname {shortname}");
				var loadedAssembly = System.AppDomain.CurrentDomain.GetAssemblies().Where(a => a.GetName().Name == shortname).FirstOrDefault();
				if (loadedAssembly != null)
					return loadedAssembly;
				Debug.Log($"[ASSRESOLVE] Not in appdomain, trying to load..");
				foreach (var dir in dirs)
				{
					if (dir == "") continue; // probably for the best
					var one = Path.Combine(dir, shortname);
					one += ".dll";
					if (File.Exists(one))
					{
						var ret = loadAssembly(one, false);
						if (ret != null) return ret;
					}
				}
				return null;
			};
		}

		void Update()
		{
			if (ts == 0)
				Debug.Log("First tick of ass loader.");
			int intv = throttle + Program.settings.assInterval;
			if (intv == 0)
				intv = 60;
			if (((ts++) % intv) != 0)
				return;
			foreach (var path in dirs)
				scanPath(path);
			foreach (var newdll in loadPending)
				loadAssembly(newdll);
			loadPending.Clear();
		}
		void scanPath(string path)
		{
			if (path == "") return;
			try
			{
				foreach (var dll in Directory.GetFiles(Path.Combine(basedir, path), "*.dll"))
				{
					if (knownDLLs.TryGetValue(dll, out Assembly sentinel) && (sentinel == null))
						continue;
					AssemblyName newer;
					try
					{
						var lts = File.GetLastWriteTime(dll);
						if (timestamps.TryGetValue(dll, out DateTime fts) && fts == lts)
							continue;
						timestamps[dll] = lts;
						newer = AssemblyName.GetAssemblyName(dll);
					}
					catch (BadImageFormatException)
					{
						Debug.Log($"{dll} doesn't look like an assembly, blacklisting.");
						knownDLLs[dll] = null;
						continue;
					}
					catch { continue; }; // keep retrying with anything else
					if (knownDLLs.TryGetValue(newer.Name, out Assembly older))
						reloadAssembly(dll, newer, older);
					else
						loadAssembly(dll);
				}
			}
			catch (FileNotFoundException) { }
			catch (DirectoryNotFoundException) { }
			catch (Exception ex) {
				Debug.Log($"[SCANPATH] {ex.ToString()}");
				throttle *= 2;
			};
		}
		void reloadAssembly(string path, AssemblyName newer, Assembly older)
		{
			if (newer.Version <= older.GetName().Version)
				return;
			foreach (var mb in monoBs[newer.Name])
				Destroy(mb);
			Trace.Log($"Unloading {older.FullName}, queueing {newer.FullName}");
			loadPending.Add(path);
		}
		Assembly loadAssembly(string file, bool run = true)
		{
			try
			{
				Debug.Log($"Trying to load {file}");
				var ass = AppDomain.CurrentDomain.Load(File.ReadAllBytes(file));
				Debug.Log($"Loaded {ass.FullName}");
				if (run)
				{
					var mblist = monoBs[ass.GetName().Name] = new List<Component>();

					foreach (var t in ass.GetExportedTypes())
					{
						if (!t.IsSubclassOf(typeof(MonoBehaviour)))
							continue;
						Debug.Log($"   Spawning {t.Name}");
						mblist.Add(pin.AddComponent(t));
					}
					Debug.Log("Done loading.");
				}
				knownDLLs[ass.GetName().Name] = ass;
				return ass;
			}
			catch (Exception ex)
			{
				throttle *= 2;
				Trace.Log(ex.ToString());
			};
			return null;
		}
		void OnApplicationQuit()
		{
			Patchwork.Program.form.Close();
		}
	}
}
	