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

namespace Patchwork
{
	public class Program
	{
		public static int version
		{
			get
			{
				return Assembly.GetExecutingAssembly().GetName().Version.Major;
			}
		}
		[DllImport("user32.dll")]
		public static extern bool EnumThreadWindows(uint dwThreadId, EnumThreadDelegate lpfn, IntPtr lParam);
		[DllImport("kernel32.dll")]
		public static extern uint GetCurrentThreadId();
		public delegate bool EnumThreadDelegate(IntPtr Hwnd, IntPtr lParam);
		[DllImport("user32.dll")]
		public static extern bool ShowWindow(IntPtr w, int cmd);
		[DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
		public static extern bool SetWindowText(IntPtr hwnd, String lpString);


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

		// Don't touch unity in weird places if local
#if !DEBUG_LOCAL
		public static string cfgpath { get { return UserData.Path + "/config.json"; } }
#else
		public static string cfgpath = "config.json";
#endif

		public static SettingsForm form;
		public static Settings _settings;
		public static Settings settings
		{
			get
			{
				if (_settings == null)
					_settings = LoadConfig();
				GameInit();
				return _settings;
			}
		}
		public static fsSerializer json;
		public static bool launched = false;

		public static Settings LoadConfig()
		{
			var s = new Settings();
			try
			{
				object se = s;
				if (json == null)
					json = new fsSerializer();
				json.TryDeserialize(fsJsonParser.Parse(File.ReadAllText(cfgpath)), typeof(Settings), ref se);
				if (se != null)
					return se as Settings;
			} catch {};
			return s;
		}

		public static void SaveConfig()
		{
			try
			{
				fsData data = null;
				settings.version++;
				json.TrySerialize(settings, out data).AssertSuccess();
				File.WriteAllText(cfgpath, fsJsonPrinter.PrettyJson(data));
			}
			catch { };
		}

		public static bool initdone = false;
		public static void GameInit()
		{
			if (initdone)
				return;
			Console.WriteLine("GameInit()");
			initdone = true;
			AssLoader.Init();
			var proc = Process.GetCurrentProcess();
			//ShowWindow(proc.MainWindowHandle, 0);
			//SetWindowText(proc.MainWindowHandle, proc.MainWindowTitle + " Patchwork Mk." + version);
			EnumThreadWindows(GetCurrentThreadId(), (W, _) =>
			{
				SetWindowText(W, UnityEngine.Application.productName + " Patchwork Mk." + version);
				ShowWindow(W, 0);
				return true;
			}, IntPtr.Zero);
			try
			{
				//System.Windows.Forms.Application.EnableVisualStyles();
				//System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);
				form = new SettingsForm(settings);
				form.Text += " Mk." + Assembly.GetExecutingAssembly().GetName().Version.Major;
				form.resolution.Items.AddRange(settings.resolutions);
				form.UpdateForm();
				form.launchButton.Click += (o, e) =>
				{
					launched = true;
					Trace.Log("Launched");
					form.Close();
				};
				if (form.ShowDialog() == DialogResult.OK)
					launched = true;
				if (launched)
				{
					form.launchButton.Enabled = false;
					form.Show();
					//form = new SettingsForm(settings);
					//form.Show();
				}
				else
					UnityEngine.Application.Quit();
				Trace.Log($"Dropping out of the initial dialog, launched={launched}");
			}
			catch (Exception ex)
			{
				MessageBox.Show(ex.ToString());
			}
		}

		public static void PostInit()
		{
			settings.Apply(true);
		}

		public static void UpdateCamera()
		{
			settings.Apply("renderingPath");
			// perhaps apply other overrides as they come up
		}

		public static void GC(string who, bool wants, object o)
		{
			Trace.Spam("[GC]" + who);
			if (!settings.lazyGC && wants)
			{
				Resources.UnloadUnusedAssets();
			}
			if (settings.lazyBundles)
				AssetBundleManager.GC();
			System.GC.Collect();
		}

		public static void GCAssets(object caller)
		{
			Trace.Spam($"[GC] Asset GC requested by {caller.GetType().Name}");
		}

	}
}
