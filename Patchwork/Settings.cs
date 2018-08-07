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
using MessagePack;
using System.Runtime.InteropServices;
using UnityStandardAssets.ImageEffects;
using Manager;

public static partial class Patchwork
{
	// The serialized fields match 1:1 to controls in SettingsForm
	[Serializable]
	public partial class Settings
	{
		public bool studioLoader = true;
		public int chaUpdateTick = -1;
		public float cumMult = 2.5f;
		public float cumLimit = 150f;
		public float cumAmount = 2.5f;
		public bool boneRoot = false;
		public bool asyncAnim = true;
		public bool fixGuides = false;
		public string scriptPath = @"bepinex;bepinex\ipa;patchwork\scripts;bepinex\core";
		public string[] scriptBlacklist = new string[]
		{
			"extensiblesaveformat",
			"resourceredirector",
			"sideloader",
			"demosaic",
			"zextensiblesaveremover",
			"sliderunlocker",
			"texrespatch",
			"ipaloader",
			"illusionplugin",
		};
		public bool studioBase = false;
		public bool nukeMat = false;
		public bool withoutManifest = false;
		public bool loadMods = false;
		public bool loadUnsafe = false;
		public bool asyncMaker = true;
		public bool abinfoCache = false;

		public bool blackFade = true;
		public List<string> scriptDisabled = new List<string>();
		public bool cacheScripts = true;
		public bool faceJPG = true;
		public bool capJPG = true;
		public bool noFrillsExit = true;
		public int nAcsSlots = 512;
		public bool spectateADV = true;
		public bool spectateH = true;
		public bool lazyAssetGC = true;
		public bool lazyGC = true;
		public bool lowPolyADV = true;
		public bool hiPoly = false;
		public bool noscopeHead = false;
		public bool noscopeClipMask = false;
		public float noscopeScale = 1;
		public float noscopeAlphaX = 0.3f;
		public float noscopeAlphaXEnd = 0.3f;
		public float noscopeAlphaY = -0.15f;
		public float noscopeAlphaYEnd = 0.43f;
		public float noscopeAlphaClamp = 0;
		public float noscopeAlphaClampEnd = 0.65f;
		public bool noscopeSim = false;
		public bool noscopeSimGomu = false;
		public byte noscopeAlphaMode;
		public bool onTop = false;
		public bool geass = false;
		public bool unlockH = false;
		public bool unlockComm = false;
		public bool benderClothes = true;
		public float HScale = 2.6f;
		public bool noTelescope = false;
		public bool cacheSprites = true;
		public string cardFmt = "Koikatu_{0}_{1}.png";
		public string ooMale = "chara/oo_base";
		public string ooFemale = "chara/oo_base";
		public string mmMale = "chara/mm_base";
		public string mmFemale = "chara/mm_base";
		public bool noBustNorm = false;
		public bool mcChange = true;
		public bool noFade = true;
		public bool skipLogo = false;
		public float rimOverride = -1;
		public int visTick = 1;
		public int shaderTick = 1;
		public bool resizable = true;
		public bool dontshow = false;
		public bool useBOM = false;
		public bool showFPS = true;
		public bool useLR = true;
		public bool fetchAssets = true;
		public bool dumpAssets = true;
		public bool whitePower = false;

		public int physLoopCount = 3;
		public int physDivisor = 1;
		public float physReflectSpeed = 1;
		public float physRate = 60;


		public bool enableStack = true;
		public bool enableSpam = false;
		public bool enableTrace = true;
		public bool enableInfo = true;
		public bool enableError = true;


		// Second tab
		public bool hideMoz = true;
		public bool fixPenis = true;
		public bool fixVagina = false;

		public bool bothMC = false;
		public bool bothClass = false;
		public bool bothFreeH = true;
		public bool bothEdit = false;

		public bool equality = true;

		public bool bgmAsync = true;
		public bool assetCache = true;
		public bool compCache = true;
		public bool assetAsync = true;
		public bool tumblr = true;
		public bool watchFolder = true;

		// Text input fields
		public float sliderMin = -1f;
		public float sliderMax = 2f;
		public float _rimG = 1.0f;
		public float shadowDistance = 80;
		public float shadowNearPlaneOffset = 4;
		public float maximumLODLevel = 0;
		public float lodBias = 2;
		public float shadowCascade2Split = (float)(1.0 / 3.0);
		public bool shadowOverride = false;
		public float customShadowStrengthTarget = 0.96f;
		public float customShadowStrengthLimit = 0.8f;

		public int particleRaycastBudget = 4096;
		public int asyncUploadTimeSlice = 4;
		public int asyncUploadBufferSize = 8;
		public int pixelLightCount = 4;
		public int maxQueuedFrames = 0;

		// Paint texture sizes
		public int bodyLowPoly = 512;
		public int bodyHiPoly = 2048;
		public int faceLowPoly = 512;
		public int faceHiPoly = 1024;
		public int eyeLowPoly = 256;
		public int eyeHiPoly = 512;

		// Dropdowns
		public byte shadows = 2;
		public byte shadowResolution = 3;
		public byte shadowProjection = 0;
		public byte shadowCascades = 2;
		public byte blendWeights = 2;
		public byte vSyncCount = 0;
		public byte masterTextureLimit = 0;
		public byte anisotropicFiltering = 1;
		public byte antiAliasing = 3;

		public bool softParticles = true;
		public bool realtimeReflectionProbes = false;
		public bool fullscreen = false;

		public byte cam_renderingPath = 1;



		// bumped each time something changes the config
		public int version;

		public string resolution = "1280x720";
		public string[] resolutions =
		{
			"854x480",
			"1024x576",
			"1136x640",
			"1280x720",
			"1366x768",
			"1538x864",
			"1600x900",
			"1680x945",
			"1920x1080",
			"2048x1152",
			"2560x1440",
			"3200x1800",
			"3840x2180"
		};
		public byte chardb = 1;
		public string[] chardbs =
		{
			"illusion.jp (jp only)|",
			"lolicore.org (world)|http://kkdb.lolicore.org:8880/char",
		};
		public byte qualitySelect = 0;

		// Map listbox selects to real values
		static Dictionary<String, int[]> parmap = new Dictionary<String, int[]>()
		{
			{ "antiAliasing" , new [] { 0,2,4,8,16,32 } },
			{ "cam_renderingPath", new [] { 0,1,2,3,4,5 } },
			{ "shadowCascades", new [] {0,2,4 } },
			{ "blendWeights", new [] {1,2,4 } },
		};

		// Apply a singular setting
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
				version++;
				SaveConfig();
			} catch (Exception ex)
			{
				Debug.Log(ex.ToString());
			}
		}

		public void SetQuality(int n)
		{
			if (qualitySelect > 0)
			{
				Debug.Log($"Setting quality to {n}");
				UnityEngine.QualitySettings.SetQualityLevel(n);
			}
		}
		public void Apply(bool setres = false)
		{
			if (initdone)
				DoApply(setres);
		}

		// CAVEAT: Fires up engine
		public void DoApply(bool setres = false)
		{
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
			if (initdone)
				DoApply(name, setres);
		}

		// CAVEAT: Fires up engine
		// setres is a bit of misnomer. if false, it means settings are applied in bulk
		public void DoApply(string name, bool setres = false)
		{
			switch (name)
			{
				case "onTop":
					form.TopMost = onTop;
					break;
				case "showFPS":
					try
					{
						Manager.Config.DebugStatus.FPS = showFPS;
					}
					catch { };
					break;

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
				default:
					if (name.StartsWith("cam_") && setres)
					{
						UpdateCamera(null);
						break;
					}
					try
					{
						var val = typeof(Settings).GetField(name);
						Debug.Log($"Updating setting for {name} = {val.GetValue(this)}");

						// shader prop
						if (name[0] == '_')
						{
							Shader.SetGlobalFloat(name, (float)val.GetValue(this));
							return;
						}

						// or at last, a renderer prop
						var prop = typeof(QualitySettings).GetProperty(name);
						if (prop == null)
						{
							Debug.Log($"Setting unknown RenderQuality property {name}");
							return;
						}

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
						Debug.Error(ex);
					}
					break;
			}
		}
	}
}
