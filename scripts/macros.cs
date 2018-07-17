// this is just dumping grounds for random in-game hacking

using IllusionUtility.GetUtility;
using static Patchwork;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using ParadoxNotion.Serialization.FullSerializer;

public class Macros : ScriptEvents {
	public bool first;
	public override bool OnScene(string name, string subname) 
	{
		if (!first)
			ShowWindow(hwnd, 5);
		first = true;
		print($"[SCENE] {name} {subname}");
		return false;
	}
}

public partial class ScriptEnv
{
	//public static HSubsConfig hscfg => HSubs.cfg;
	public static Lookat_dan lookatdan => Object.FindObjectOfType<Lookat_dan>();
	//public static GameObject female => GameObject.Find("chaF_001");
	public static GameObject female => SceneManager.GetSceneAt(0).GetRootGameObjects()[0].transform.FindLoop("chaF_001");
	public static ChaFileStatus fstat => GameObject.Find("chaF_001").GetComponent<ChaControl>().fileStatus;
	public static bool ftog => fstat.visibleBodyAlways = !fstat.visibleBodyAlways;
	//public static GameObject male => GameObject.Find("chaM_001");
	public static GameObject male => SceneManager.GetSceneAt(0).GetRootGameObjects()[0].transform.FindLoop("chaM_001");
	public static ChaControl control => Object.FindObjectOfType(typeof(ChaControl)) as ChaControl;
	public static Manager.Scene scene = Singleton<Manager.Scene>.Instance;
	public static Manager.Game game => Singleton<Manager.Game>.Instance;

	public static bool firsteval;
	public static new object eval(string str)
	{
		if (!firsteval)
			Script.eval("using System.Linq; using System.Collections.Generic; using System.Collections; using UnityEngine; using UnityEngine.SceneManagement;");
		firsteval = true;
		return Script.eval(str);
	}

	public static ActionScene act => Object.FindObjectOfType(typeof(ActionScene)) as ActionScene;
	public static object clear
	{
		get
		{
			form.replOutput.Text = "";
			return typeof(Sentinel);
		}
	}
}

