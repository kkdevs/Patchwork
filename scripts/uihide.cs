using UnityEngine;

public class UIhide : MonoBehaviour {
	public static bool state;
	public void Update()
	{
		if (Input.GetKeyDown(KeyCode.Space))
			if (ScriptEnv.scene.NowSceneNames[0] == "HProc")
				GameObject.Find("Canvas").GetComponent<Canvas>().enabled = state = !state;
	}
}
