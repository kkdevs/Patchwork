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

namespace Patchwork
{
	public static class Program
	{
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
		public static Settings settings = new Settings();
		public static fsSerializer json;
		public static bool launched = false;

		public static bool LoadConfig()
		{
			try
			{
				object se = settings;
				json.TryDeserialize(fsJsonParser.Parse(File.ReadAllText(cfgpath)), typeof(Settings), ref se);
				if (se != null)
					settings = se as Settings;
			}
			catch
			{
				return false;
			};
			return true;
		}

		public static void SaveConfig()
		{
			fsData data = null;
			json.TrySerialize(settings, out data).AssertSuccess();
			File.WriteAllText(cfgpath, fsJsonPrinter.PrettyJson(data));
		}

		// Bepinex specific hacks
		public static Assembly benis;
		public static Type benis_type;
		public static FieldInfo benis_loaded;
		static string late_log;

		public static int Main(string[] args)
		{
			return 0;
		}

		public static bool initdone = false;
		public static void GameInit()
		{
			if (initdone)
				return;
			initdone = true;

			json = new fsSerializer();
			try
			{
				//System.Windows.Forms.Application.EnableVisualStyles();
				//System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);
				form = new SettingsForm(settings);
				form.Text += " v" + Assembly.GetExecutingAssembly().GetName().Version.Major;
				LoadConfig();
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
				}
				else
					UnityEngine.Application.Quit();
				Trace.Log($"Dropping out of the initial dialog, launched={launched}");
			}
			catch (Exception ex)
			{
				MessageBox.Show(ex.ToString());
			}

			settings.Apply(true);
		}

		public static void UpdateCamera()
		{
			settings.Apply("renderingPath");
			// perhaps apply other overrides as they come up
		}

	}
}
