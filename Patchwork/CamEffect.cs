using Manager;
using UnityEngine;
using UnityStandardAssets.ImageEffects;

namespace Patchwork
{
	public partial class Settings
	{
		public float cam_aoeRadius = 1f;
		public float cam_amplColorExposure = 1f;
		public bool cam_useFog;
		public bool cam_useAmplifyColor;
		public bool cam_useAmplifyOcclus;
		public bool cam_useBloom;
		public bool cam_useSunShafts;
		public bool cam_useVignette;
		public bool cam_useDOF;
		public bool cam_useBlur;
		public bool cam_useCrossFade;
		public bool cam_useSepia;

		public bool cam_ppOverride = false;

		public void DoUpdateCamera(Camera cam, bool fix = false)
		{
			if (!Program.initdone)
				return;
			UpdateCamera(cam, fix);
		}
		public void UpdateCamera(Camera cam, bool fix = false)
		{
			if (cam == null)
				cam = Game.Instance?.nowCamera;
			if (cam == null)
			{
				cam = Camera.main;
			}
			if (cam == null)
				return;
			cam.renderingPath = (RenderingPath)cam_renderingPath;
			if (Game.Instance?.nowCamera != null)
				Game.Instance.nowCamera.renderingPath = (RenderingPath)cam_renderingPath;
			Trace.Spam($"[CAM] Using camera {cam.name}");
			/*var e = cam.GetComponent<CameraEffector>();
			if (e == null)
			{
				Debug.Log($"[CAM] Effector not found, hmm, fix={fix}...");
			}*/
			if (!cam_ppOverride) return;
			var ac = cam.GetComponent<AmplifyColorEffect>();
			if (ac != null)
			{
				ac.enabled = cam_useAmplifyColor;
				ac.BlendAmount = 1f - cam_amplColorExposure;
			}
			var aoe = cam.GetComponent<AmplifyOcclusionEffect>();
			if (aoe != null)
			{
				aoe.enabled = cam_useAmplifyOcclus;
				aoe.Radius = cam_aoeRadius;
			}
			var bloom = cam.GetComponent<BloomAndFlares>();
			if (bloom != null)
			{
				bloom.enabled = cam_useBloom;
			}
			var crossfade = cam.GetComponent<CrossFade>();
			if (crossfade != null)
			{
				crossfade.enabled = cam_useCrossFade;
			}
			var dof = cam.GetComponent<DepthOfField>();
			if (dof != null)
			{
				dof.enabled = cam_useDOF;
			}
			var fog = cam.GetComponent<GlobalFog>();
			if (fog != null)
			{
				fog.enabled = cam_useFog;
			}
			var sepia = cam.GetComponent<SepiaTone>();
			if (sepia != null)
			{
				sepia.enabled = cam_useSepia;
			}
			var sunshafts = cam.GetComponent<SunShafts>();
			if (sunshafts != null)
			{
				sunshafts.enabled = cam_useSunShafts;
			}
			var vignette = cam.GetComponent<VignetteAndChromaticAberration>();
			if (vignette != null)
			{
				vignette.enabled = cam_useVignette;
			}

			foreach (var comp in cam.gameObject.GetComponents<Component>())
			{
				var t = comp.GetType();
				Trace.Spam($"[CAM] {t.Name} : {t.BaseType?.Name}");
			}
		}

	}
}
