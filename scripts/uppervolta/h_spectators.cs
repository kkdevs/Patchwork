// upillu3352.zip[KK] GOLマクロ集(全員脱衣、衆人環視H、女の子回転、スタジオキャラ一括変更)v.1.0

// TBD: just skip re-loading the map (which deactives overworld charas) altogether
// and drop straight into H - HSceneProc.cs:650

using System.Linq;
using UnityEngine;
using Manager;
using System.Reflection;

public class HOnlookers : MonoBehaviour
{
	public static SaveData.Heroine curHeroine = null;

	public void Update()
	{
		var actionScene = Singleton<ActionScene>.Instance;
		curHeroine = GetCurrentAdvHeroine() ?? curHeroine;

		if (curHeroine != null && Singleton<HScene>.Instance != null)
		{
			var npcs = actionScene.npcList
				.Where(x => x.mapNo == actionScene.Map.no && x.heroine != curHeroine);

			foreach (var npc in npcs)
			{
				if (!npc.isActive)
				{
					//Console.WriteLine(npc.name + "/" + npc.AI.actionNo);
					npc.SetActive(true);
					//npc.AI.Reset(false);
				}
			}
		}
	}

	public SaveData.Heroine GetCurrentAdvHeroine()
	{
		SaveData.Heroine result;
		try
		{
			var actScene = Singleton<Game>.Instance.actScene;
			MonoBehaviour nowScene;

			if (actScene == null)
			{
				nowScene = null;
			}
			else
			{
				var advScene = actScene.AdvScene;
				nowScene = ((advScene != null) ? advScene.nowScene : null);
			}

			MonoBehaviour monoBehaviour = nowScene;
			SaveData.Heroine heroine = ((monoBehaviour != null) ? monoBehaviour.GetType().GetField("m_TargetHeroine", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(monoBehaviour) : null) as SaveData.Heroine;
			result = heroine;
		}
		catch
		{
			result = null;
		}
		return result;
	}
}
