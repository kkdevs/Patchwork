using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using ParadoxNotion.Serialization.FullSerializer;
using UnityEngine;
using System.Threading;
using System.Runtime.InteropServices;
using UnityEngine;
using System.Diagnostics;
using MessagePack;
using System.Text;
using System.Configuration;
using System.Xml.Serialization;
using System.Drawing;
using UnityEngine.Rendering;
using UnityStandardAssets.ImageEffects;
using Manager;

namespace Patchwork
{
	public static partial class Program
	{
		public static int version
		{
			get
			{
				return Assembly.GetExecutingAssembly().GetName().Version.Major;
			}
		}

		public static string mkver
		{
			get
			{
				var ver = Assembly.GetExecutingAssembly().GetName().Version;
				return " Mk." + ver.Major + (ver.Minor != 0 ? $"({ver.Minor})" : "");
			}
		}

		public static bool isStudio
		{
			get
			{
				return UnityEngine.Application.productName == "CharaStudio";
			}
		}
		public static string prefix { get {
				return isStudio ? "Studio" : "";
			}
		}

		public static string BasePath;

		public static string dumppath { get { return BasePath + "patchwork/mod/!base/"; } }
		public static string cfgpath { get { return BasePath + "patchwork/config.xml"; } }
		public static string historypath { get { return BasePath + "patchwork/history.xml"; } }
		public static string modbase { get { return Path.GetFullPath(BasePath + "patchwork/mod/"); } }
		public static string tempbase { get { return Path.GetFullPath(BasePath + "patchwork/cache/"); } }
		public static SettingsForm form;
		public static Settings _settings;
		public static Settings settings
		{
			get
			{
				if (_settings == null)
					_settings = LoadConfig();
				return _settings;
			}
		}
		public static fsSerializer _fsjson;
		public static fsSerializer fsjson {
			get {
				if (_fsjson == null)
				{
					_fsjson = new fsSerializer();
				}
				return _fsjson;
			}
		}

		public static bool launched = false;
		public static bool exiting = false;
		public static bool earlydone = false;

		public static void ConfigDialog()
		{
			bool runStudio = Environment.GetEnvironmentVariable("KK_RUNSTUDIO") != null;
			if (((!runStudio) && File.Exists(BasePath + "Koikatu_Data/Managed/bepinex.dll")) || (runStudio && File.Exists(BasePath + "CharaStudio_data/Managed/bepinex.dll")))
			{
				splash?.Close();
				MessageBox.Show("This release can't run under bepinex 3 anymore, please update to bepinex 4.");
				ExitProcess(1);
			}
			//System.Windows.Forms.Application.EnableVisualStyles();
			System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(true);
			if (settings.dontshow)
			{
				launched = true;
				return;
			}

			form = new SettingsForm(settings);
			form.Text += mkver;

			form.resolution.Items.AddRange(settings.resolutions);
			foreach (var n in settings.chardbs)
				form.chardb.Items.Add(n.Split('|')[0]);
			InitScripts();
			form.UpdateForm();
			splash?.Close();
			if (runStudio)
			{
				form.Show();
				launched = true;
				return;
			}
			form.scriptReload.Click += (o, e) =>
			{
				DiscoverScripts();
				form.UpdateForm();
			};
			form.launchButton.Click += (o, e) =>
			{
				form.Close();
				launched = true;
			};
			form.runChara.Click += (o, e) =>
			{
				form.Close();
				SaveConfig();
				Environment.SetEnvironmentVariable("KK_RUNSTUDIO", "1");
				Process.Start(exename);
				Environment.Exit(0);
			};
			if (form.ShowDialog() == DialogResult.OK)
				launched = true;
			SaveConfig();
			if (!launched)
				Environment.Exit(1);
			form.launchButton.Enabled = false;
			form.runChara.Enabled = false;
			//foreach (var c in form.tabPage7.Controls)
			//	(c as Control).Enabled = false;
			form.scriptReload.Enabled = false;
			form.Show();
			form.tabControl1.SelectedIndex = form.tabControl1.Controls.Count - 1;
			form.FormClosing += (o, e) =>
			{
				//if (e.CloseReason == CloseReason.ApplicationExitCall)
				if (!exiting)
					e.Cancel = true;
			};
		}
		public static string exename;
		public static bool initConfig;

		public static void InitConfig()
		{
			if (initConfig)
				return;
			Ext.Init();
			initConfig = true;
			exename = Environment.GetEnvironmentVariable("PATCHWORK_EXE");
			if (exename == null)
			{
				var fn = new StringBuilder(256);
				GetModuleFileName(IntPtr.Zero, fn, fn.Capacity);
				exename = fn.ToString();
			}
			BasePath = Path.GetDirectoryName(exename) + "/";
			LoadConfig();
			Directory.CreateDirectory(modbase);
			Directory.CreateDirectory(tempbase);
			Directory.CreateDirectory(dumppath);
			Trace.Info("Basepath=" + BasePath);
		}

		public static Splash splash;
		public static int Main(string[] args)
		{
			splash = new Splash();
			splash.Show();
			splash.Refresh();
			InitConfig();
			ConfigDialog();
			earlydone = true;
			return 0;
		}

		public static void FixWindow()
		{
			if (settings.resizable && !UnityEngine.Screen.fullScreen)
			{
				var ow = GetWindowLongPtr(hwnd, -16);
				SetWindowLongPtr(hwnd, -16, ow | 0x00040000L | 0x00010000L);
			}
		}

		public static Settings LoadConfig()
		{
			Settings s = null;
			XmlSerializer x = new XmlSerializer(typeof(Settings));
			try
			{
				using (var f = new StreamReader(File.Open(cfgpath, FileMode.Open), UTF8Encoding.UTF8))
					s = x.Deserialize(f) as Settings;
			}
			catch (Exception ex)
			{
#if GAME_DEBUG
				MessageBox.Show(ex.ToString(), "Config error", MessageBoxButtons.OK);
#endif
			}
			return s ?? new Settings();
		}

		public static void SaveBytes<T>(string path, T data) {
			var fn = BasePath + "/UserData/" + path;
			File.WriteAllBytes(fn, LZ4MessagePackSerializer.Serialize<T>(data));
			File.WriteAllBytes(fn + ".json", System.Text.Encoding.UTF8.GetBytes(MessagePackSerializer.ToJson(data)));
		}

		public static T LoadBytes<T>(string path) where T : class
		{
			var fn = BasePath + "/UserData/" + path;
			return LZ4MessagePackSerializer.Deserialize<T>(File.ReadAllBytes(fn));
		}

		public static void SaveConfig()
		{
			if (form != null && form.updating)
				return;
			try
			{
				XmlSerializer x = new XmlSerializer(typeof(Settings));
				using (var f = File.Open(cfgpath, FileMode.Create))
					x.Serialize(f, settings);
			}
			catch (Exception ex) {
				Trace.Error("Saving config failed with " + ex.ToString());
			};
		}

		public static void DoExit()
		{
			if (settings.noFrillsExit)
				ExitProcess(0);
			try
			{
				timer.Stop();
				timer.Dispose();
			}
			catch { };
			try
			{
				launched = false;
				exiting = true;
				Program.form.Close();
			}
			catch { };
			for (int i = 0; i < 1000; i++)
				System.Windows.Forms.Application.DoEvents();		
		}

		public static IntPtr hwnd = (IntPtr)(-1);

		public static bool initdone = false;
		public static void AssertLate(string by="")
		{
			if (!earlydone && initConfig)
			{
				var msg = "AssertLate failed from: " + by + "\n" + Environment.StackTrace;
				Console.WriteLine(msg);
				Console.Out.Flush();
				MessageBox.Show(msg, "Fatal error", MessageBoxButtons.OK);
				Environment.Exit(1);
			}
		}

		static bool blinit;
		public static void InitBeforeBaseLoader()
		{
			if (!blinit)
			{
				form?.BringToFront();
				form?.replOutput?.AppendText("Loading...\n");
				System.Windows.Forms.Application.DoEvents();
			}
			blinit = true;
		}

		public static System.Timers.Timer timer;
		public static void PostInit()
		{
			earlydone = true;
			if (!initConfig)
			{
				InitConfig(); // If we're running standalone
				ConfigDialog();
			}
			initdone = true;
			timer = new System.Timers.Timer(100);
			timer.AutoReset = true;
			timer.Elapsed += (o, e) =>
			{
				FixWindow();
			};
			timer.Start();

			settings.Apply(true);
			settings.UpdateCamera(null);
			SaveConfig();
			form?.BringToFront();

			AppDomain.CurrentDomain.AssemblyResolve += (s, args) =>
			{
				var shortname = new System.Reflection.AssemblyName(args.Name).Name;
				Debug.Log("something is looking for " + shortname);
				var loadedAssembly = System.AppDomain.CurrentDomain.GetAssemblies().Where(a => a.GetName().Name == shortname).FirstOrDefault();
				if (loadedAssembly != null)
				{
					return loadedAssembly;
				}
				return null;
			};

			// Load history buffer
			List<string> hist = null;
			XmlSerializer x = new XmlSerializer(typeof(List<string>));
			try
			{
				using (var f = new StreamReader(File.Open(historypath, FileMode.Open), UTF8Encoding.UTF8))
					if ((hist = (x.Deserialize(f)) as List<string>) != null)
						foreach (var line in hist)
							form.replInput.history.AddLast(line);
			}
			catch (Exception ex)
			{
				Debug.Log(ex);
			}

			// Fire up scripts as well the repl
			Script.Reporter.write = (s) =>
			{
				form.replOutput.AppendText(s);
				if (s.Trim() != "")
					Trace.Log(s.Trim());
				System.Windows.Forms.Application.DoEvents();
			};
			if (Script.reload())
			{
				form.replInput.Print = (s) => Script.Invoke("pp", s);
				form.replInput.Eval = (s) =>
				{
					using (var f = File.Open(historypath, FileMode.Create))
						x.Serialize(f, form.replInput.history.ToList());
					Script.Invoke("print", "csharp> " + s);
					return Script.Invoke("eval", s);
				};
				form.replInput.Sentinel = typeof(Script.Sentinel);
				form.replInput.GetCompletions = (s) =>
				{
					string prefix;
					var ret = new List<string>();
					try
					{
						var arr = Script.Evaluator.GetCompletions(s, out prefix);
						if (arr != null)
							foreach (var sug in arr)
								ret.Add(s + sug);
					}
					catch (Exception ex)
					{
						Debug.Log("Auto-complete exception: " + ex.ToString());
					}
					return ret;
				};
			} else
			{
				Trace.Error("[SCRIPT] Failed to initialize script state.");
			}

			// Fix up window
			var proc = Process.GetCurrentProcess();
			EnumThreadWindows(GetCurrentThreadId(), (W, _) => {
				var sb = new System.Text.StringBuilder(256);
				GetClassName(W, sb, 256);
				if (sb.ToString() == "UnityWndClass")
					hwnd = W;
				return true;
			}, IntPtr.Zero);
			SetWindowText(hwnd, UnityEngine.Application.productName + mkver);
			System.Windows.Forms.Application.ApplicationExit += (s, e) =>
			{
				Environment.Exit(0);
			};
		}

		public static void InitScripts()
		{
			AppDomain.CurrentDomain.AssemblyResolve += (o, e) =>
			{
				var self = Assembly.GetExecutingAssembly();
				var fn = new AssemblyName(e.Name).Name;
				if (settings.scriptBlacklist.Contains(fn.ToLower())) {
					Debug.Log("Redirecting " + fn);
					return self;
				}
				foreach (var p in settings.scriptPath.Split(';')) {
					Assembly nass = null;
					try
					{
						var ffn = Path.Combine(BasePath, p) + "/" + fn + ".dll";
						Debug.Log("Loading " + ffn);
						nass = Ext.LoadAssembly(ffn);
					} catch { };
					if (nass != null)
						return nass;
				}
				return self.GetTypesSafe().Any((t) => t.Namespace == fn) ? self : null;
			};
			DiscoverScripts();
		}

		public static void DiscoverScripts()
		{
			ScriptEntry.list.Clear();
			Dictionary<string, string> dupes = new Dictionary<string, string>();
			foreach (var f in Ext.GetFilesMulti(settings.scriptPath.Split(';').Select(x => BasePath + x), "*.*")) {
				var bn = Path.GetFileNameWithoutExtension(f);
				var bnl = bn.ToLower();
				if (settings.scriptBlacklist.Contains(bnl))
				{
					Trace.Log($"[SCRIPT] Skipping {bn} because it is blacklisted");
					continue;
				}
				if (dupes.TryGetValue(bnl, out string existing))
				{
					// replace dll with script if same name
					if (f.EndsWith(".cs") && !existing.EndsWith(".cs"))
					{
						Trace.Log($"[SCRIPT] Skipping {existing} in favor of {f}");
					}
					else
					{
						Trace.Log($"[SCRIPT] Skipping duplicate {f}, previously seen as {dupes[bnl]}");
						continue;
					}
				}
				dupes[bnl] = f;
			}

			foreach (var f in dupes.Values)
			{
				var bn = Path.GetFileNameWithoutExtension(f);
				var entry = new ScriptEntry()
				{
					name = bn,
					source = f,
					enabled = !settings.scriptDisabled.Contains(bn.ToLower()),
				};
				entry.Add();
			}
		}


		public static void GC(string who, bool asset, bool heap, object o)
		{
			if (Singleton<Character>.Instance != null && !Singleton<Character>.Instance.enableCharaLoadGCClear)
				return;
			Trace.Spam("[GC] Requested by " + who);
			if (asset && !settings.lazyAssetGC) {
#if !USE_OLD_ABM
				LoadedAssetBundle.Flush(true);
#endif
				Resources.UnloadUnusedAssets();
			}
			if (heap && !settings.lazyGC)
				System.GC.Collect();
		}

		public static void GCHeap(object caller)
		{
			GC(caller.GetType().Name, false, true, caller);
		}

		public static void GCAll(object caller, bool weak = false)
		{
			if (weak)
				return;
			GC(caller.GetType().Name, true, true, caller);
		}

		public static void GCAssets(object caller)
		{
			GC(caller.GetType().Name, true, false, caller);
		}

		public static bool geass => settings.geass && Input.GetMouseButton(1);

	}
}
