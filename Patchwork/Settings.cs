using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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

namespace Patchwork
{
	// The serialized fields match 1:1 to controls in SettingsForm
	[Serializable]
	public class Settings
	{
		// Second tab
		public bool hideMoz = true;
		public bool fixPenis = true;
		public bool fixVagina = false;

		public bool bothClass = false;
		public bool bothFreeH = true;
		public bool bothEdit = true;

		public bool equality = true;

		public bool bgmAsync = true;
		public bool lazyGC = true;
		public bool lazyBundles = true;
		public bool tumblr = true;
		public bool watchFolder = true;

		// Text input fields
		public float shadowDistance = 50;
		public float shadowNearPlaneOffset = 4;
		public float maximumLODLevel = 0;
		public float lodBias = 2;
		public float shadowCascade2Split = (float)(1.0 / 3.0);
		public float customShadowStrengthTarget = 0.96f;
		public float customShadowStrengthLimit = 0.8f;

		public int particleRaycastBudget = 4096;
		public int asyncUploadTimeSlice = 2;
		public int asyncUploadBufferSize = 4;
		public int pixelLightCount = 4;
		public int maxQueuedFrames = 8;

		// Dropdowns
		public byte shadows = 2;
		public byte shadowResolution = 3;
		public byte shadowProjection = 1;
		public byte shadowCascades = 1;
		public byte blendWeights = 2;
		public byte vSyncCount = 0;
		public byte masterTextureLimit = 0;
		public byte anisotropicFiltering = 1;
		public byte antiAliasing = 3;

		public bool softParticles = false;
		public bool realtimeReflectionProbes = false;
		public bool fullscreen = false;

		public byte renderingPath = 1;

		public bool unlockH = true;

		// bumped each time something changes the config
		public int version;

		public string resolution = "1280x720";
		public string[] resolutions =
		{
			"854x480",
			"1024x576",
			"1136x640",
			"1280x720",
			"1280x800",
			"1366x768",
			"1538x864",
			"1600x900",
			"1680x1050",
			"1920x1080",
			"1920x1200",
			"2048x1152",
			"2560x1440",
			"3200x1800",
			"3840x2180"
		};
		public byte qualitySelect = 1;

		// Map listbox selects to real values
		static Dictionary<String, int[]> parmap = new Dictionary<String, int[]>()
		{
			{ "antiAliasing" , new [] { 0,2,4,8,16,32 } },
			{ "renderingPath", new [] { 0,1,2,3,4,5 } },
			{ "shadowCascades", new [] {0,2,4 } },
			{ "blendWeights", new [] {1,2,4 } },
		};


		// Maps non-linear listboxes
		public void Update(string name, object val)
		{
			try
			{
				var f = typeof(Settings).GetField(name);
				if (f.FieldType == typeof(int))
				{
					val = (int)(float)val;
				}
				// everything else already converted by caller
				f.SetValue(this, val);
				Apply(name, true);
				Program.SaveConfig();
			} catch (Exception ex)
			{
				Trace.Log(ex.ToString());
			}
		}

		public void SetQuality(int n)
		{
			if (qualitySelect > 0)
			{
				Trace.Log($"Setting quality to {n}");
				UnityEngine.QualitySettings.SetQualityLevel(n);
			}
		}

		public void Apply(bool setres = false)
		{
			if (!Program.launched) return;
			if (qualitySelect > 0)
			{
				SetQuality((((int)qualitySelect) - 1)*2);
				Apply("resolution", setres);
				Apply("fullscreen", setres);
			}
			else
			{
				foreach (var f in typeof(Settings).GetFields())
				{
					Apply(f.Name, setres);
				}
			}
		}

		public void Apply(string name, bool setres = false)
		{
			if (!Program.launched) return;
			switch (name)
			{
				case "fullscreen":
					if (setres)
						UnityEngine.Screen.fullScreen = fullscreen;
					break;
				case "resolution":
					if (setres)
					{
						try
						{
							var wh = resolution.Split('x');
							UnityEngine.Screen.SetResolution(int.Parse(wh[0]), int.Parse(wh[1]), fullscreen);
						}
						catch { };
					}
					break;
				case "renderingPath":
					Trace.Log($"Changing rendering path, new = {renderingPath}");
					var path = (RenderingPath)renderingPath;
					if (Camera.main != null)
						Camera.main.renderingPath = path;
					if (Camera.current != null)
						Camera.current.renderingPath = path;

					break;
				default:
					try
					{
						var prop = typeof(QualitySettings).GetProperty(name);
						if (prop == null)
						{
							Trace.Error($"Setting unknown RenderQuality property {name}");
							return;
						}
						Trace.Log($"Updating setting for {name}");
						var val = typeof(Settings).GetField(name);

						object setting = val.GetValue(this);
						if (setting is byte && parmap.TryGetValue(name, out int[] map))
						{
							setting = map[(byte)setting];
						}
						var pt = prop.PropertyType;
						if (pt.IsEnum)
						{
							setting = Enum.ToObject(pt, setting);
						}
						else
						{
							setting = Convert.ChangeType(setting, pt);
						}
						prop.SetValue(null, setting, null);
					}
					catch (Exception ex)
					{
						Trace.Log(ex.ToString());
					}
					break;
			}
		}
	}
}
