//@INFO: Banked accessory slots
//@DESC: For each coordinate there is 25 banks of 20 slots each
//@VER: 1

using ChaCustom;
using UnityEngine;
using UnityEngine.SceneManagement;
using static ScriptEnv;
using UniRx;

public class MoreACS : GhettoUI {
	public CustomBase custom;
	public CustomAcsChangeSlot acsmenu;
	public int selno;

	public override void OnCustom(CustomBase c)
	{
		acsmenu = Object.FindObjectOfType<CustomAcsChangeSlot>();
		custom = c;
	}

	public override void OnGUI()
	{
		if (scene.NowSceneNames[0] != "CustomScene") return;
		var ctrl = custom?.customCtrl;
		var acsbtn = ctrl?.cvgCustomUI?.transform.Find("CvsMainMenu/BaseTop/tglAccessories");
		if (acsbtn == null)
			return;
		if (!acsbtn.gameObject.GetComponent<UnityEngine.UI.Toggle>().isOn)
			return;
		var cha = custom.chaCtrl;
		GUILayout.BeginArea(new Rect(Screen.width * 0.12f, 0, Screen.width * 0.205f, Screen.height));
		GUILayout.FlexibleSpace();
		GUILayout.BeginHorizontal();
		for (int i = 0; i < 25; i++)
		{
			if (i % 5 == 0)
			{
				GUILayout.EndHorizontal();
				GUILayout.BeginHorizontal();
			}
			if (i == selno) {
				GUILayout.Button($"{(i*20+1):000}", selected);
				continue;
			}
			if (!GUILayout.Button($"{(i*20+1):000}", unselected))
				continue;
			selno = i;
			// switch bank of this coorde
			cha.acsBank = selno;
			cha.nowCoordinate.bank = selno;
			cha.ChangeCoordinateType((ChaFileDefine.CoordinateType)cha.fileStatus.coordinateType, false);
			custom.updateCustomUI = true;
		}
		GUILayout.EndHorizontal();
		GUILayout.EndArea();
	}
}