using IllusionUtility.GetUtility;
using UnityEngine;
using UnityEngine.SceneManagement;

public partial class ScriptEnv
{
	public static Lookat_dan lookatdan => Object.FindObjectOfType<Lookat_dan>();
	public static GameObject female1 => SceneManager.GetSceneAt(0).GetRootGameObjects()[0].transform.FindLoop("chaF_001");
}

