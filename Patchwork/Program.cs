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

namespace Patchwork
{
	public partial class Program
	{
		public static int version
		{
			get
			{
				return Assembly.GetExecutingAssembly().GetName().Version.Major;
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

		// Don't touch unity in weird places if local
		public static string cfgpath { get { return UserData.Path + "/config.json"; } }
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

		public static void FixWindow()
		{
			if (settings.resizable)
			{
				var ow = GetWindowLongPtr(hwnd, -16);
				SetWindowLongPtr(hwnd, -16, ow | 0x00040000L | 0x00010000L);
			}
		}

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

		public static IntPtr hwnd = (IntPtr)(-1);

		public static bool initdone = false;
		public static void GameInit()
		{
			if (initdone)
				return;
			Console.WriteLine("GameInit()");
			initdone = true;

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

			Script.Init();
			var proc = Process.GetCurrentProcess();

			EnumThreadWindows(GetCurrentThreadId(), (W, _) =>
			{
				var sb = new System.Text.StringBuilder(256);
				GetClassName(W, sb, 256);
				if (sb.ToString() == "UnityWndClass")
					hwnd = W;
				return true;
			}, IntPtr.Zero);
			SetWindowText(hwnd, UnityEngine.Application.productName + " Mk." + version);
			ShowWindow(hwnd, 0);

			FixWindow();
			try
			{
				if (!settings.dontshow)
				{
					form = new SettingsForm(settings);
					form.Text += " Mk." + Assembly.GetExecutingAssembly().GetName().Version.Major;
					form.resolution.Items.AddRange(settings.resolutions);
					foreach (var n in settings.chardbs)
						form.chardb.Items.Add(n.Split('|')[0]);
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
				}
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
