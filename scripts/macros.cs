// this is just dumping grounds for random in-game hacking

using IllusionUtility.GetUtility;
using Patchwork;
using UnityEngine;
using UnityEngine.SceneManagement;

public partial class ScriptEnv
{
	public static Lookat_dan lookatdan => Object.FindObjectOfType<Lookat_dan>();
	//public static GameObject female => GameObject.Find("chaF_001");
	public static GameObject female => SceneManager.GetSceneAt(0).GetRootGameObjects()[0].transform.FindLoop("chaF_001");
	public static ChaFileStatus fstat => GameObject.Find("chaF_001").GetComponent<ChaControl>().fileStatus;
	public static bool ftog => fstat.visibleBodyAlways = !fstat.visibleBodyAlways;
	//public static GameObject male => GameObject.Find("chaM_001");
	public static GameObject male => SceneManager.GetSceneAt(0).GetRootGameObjects()[0].transform.FindLoop("chaM_001");
	public static ChaControl control => Object.FindObjectOfType(typeof(ChaControl)) as ChaControl;
	public static Manager.Scene scene = Singleton<Manager.Scene>.Instance;

	public static void SetDankon(ChaControl cha)
	{
		print("set dan alpha");
		var dan = cha.objBody.transform.FindLoop("o_dankon")?.GetComponent<Renderer>();
		if (dan == null)
		{
			print("no diq");
			return;
		}
		if (!Program.settings.noTelescope)
		{
			print("disab");
			dan.material.SetTexture(ChaShader._AlphaMask, null);
			return;
		}
		var t2d = new Texture2D(2, 2);

		t2d.SetPixel(0, 0, Color.white);
		t2d.SetPixel(0, 1, Color.black);
		t2d.SetPixel(1, 0, Color.black);
		t2d.SetPixel(1, 1, Color.white);
		t2d.Apply();
		dan.material.SetTexture(ChaShader._AlphaMask, t2d);
		dan.material.SetTextureOffset(ChaShader._AlphaMask, new Vector2(Program.settings.noscopeAlphaX, Program.settings.noscopeAlphaY));
		dan.material.SetTextureScale(ChaShader._AlphaMask, new Vector2(Program.settings.noscopeScale, Program.settings.noscopeScale));

	}

	public static int tick;
	public static void calcDan(Lookat_dan dan)
	{
		if (Program.settings.noscopeClipMask && !Program.settings.noscopeSim)
		{
			var danbase = dan.objDanBase.transform.position;
			var kokan = dan.transLookAtNull.transform.position;
			var tip = dan.objDanTop.transform.position;
			var basetokokan = (kokan - danbase).magnitude;
			var kokantotip = (kokan - tip).magnitude;
			var progress = Mathf.InverseLerp(basetokokan + kokantotip, 0, basetokokan);
			if (tick++ % 60 == 0)
			{
				print(basetokokan);
				print(kokantotip);
				print(basetokokan+kokantotip);
				print($"Progress {progress}");
			}
			dan.male.SetDankonClipVars(dan.renDan, progress);
		}
	}

}

