//@INFO: Nicer game save/load
//@VER: 1

// eye bleach alert

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ActionGame;
using Manager;
using Patchwork;
using UniRx;
using UnityEngine;
using UnityEngine.UI;
using static Manager.Scene.Data;

public class GhettoSave : ScriptEvents
{
	public string datefmt = "MM/dd/yyyy hh:mm:ss";
	public string scene;
	public List<GraphicRaycaster> casters = new List<GraphicRaycaster>();
	public void disableRaycasts()
	{
		foreach (var c in Object.FindObjectsOfType<GraphicRaycaster>())
		{
			if (c.enabled)
				casters.Add(c);
			c.enabled = false;
		}
		print(casters.Count);
	}
	public void enableRaycasts()
	{
		scene = null;
		ask = null;
		maybesave = null;
		foreach (var c in casters)
			c.enabled = true;
		casters.Clear();
	}
	public List<KeyValuePair<string, System.DateTime>> saves = new List<KeyValuePair<string, System.DateTime>>();
	public void reloadSaves()
	{
		saves.Clear();
		foreach (var f in Ext.GetFilesMulti(new[] { savedir, (scene == "Save") ? "nonexistent" : savedir2 }, "*.dat"))
			saves.Add(new KeyValuePair<string, System.DateTime>(f, File.GetLastWriteTime(f)));

		saves.Sort((a, b) =>
		{
			return (int)(b.Value - a.Value).TotalSeconds;
		});

	}

	public override bool OnScene(string name, string subname)
	{
		if (name == "Load" || name == "Save")
		{
			print("Entering ghetto");
			scene = name;
			disableRaycasts();
			reloadSaves();
			return true;
		}
		return false;
	}
	public Vector2 scrollpos;
	public GUISkin skin;
	public GUIStyle toleft;
	public string savedir => Application.dataPath + "/../userdata/save/ghettosave";
	public string savedir2 => Application.dataPath + "/../userdata/save/game";
	public override void Start()
	{
		Directory.CreateDirectory(savedir);
	}
	public override void Occasion()
	{
		foreach (var c in casters)
			c.enabled = false;
	}

	public string ask;
	public string maybesave;
	public override void OnGUI()
	{
		if (!Singleton<Manager.Scene>.Instance.isGameEndCheck)
			return;
		var e = Event.current;
		if (e.isKey && ScriptEnv.scene.NowSceneNames.FirstOrDefault() == "Action" && Input.GetKeyDown(KeyCode.F6))
		{
			if (scene == null)
			{
				if (ScriptEnv.act != null)
					ScriptEnv.act._isCursorLock = false;
				scene = "Save";
				disableRaycasts();
				reloadSaves();
			}
		}
		if (e.isMouse && e.button == 1)
		{
			if (ask == null)
				enableRaycasts();
			ask = null;
		}
		if (scene == null) return;
		if (skin == null)
		{
			Texture2D tex, tex2, tex3;
			tex = new Texture2D(1, 1);
			tex.SetPixel(0, 0, new Color(0, 0, 0, 0.7f));
			tex.Apply();

			tex2 = new Texture2D(1, 1);
			tex2.SetPixel(0, 0, new Color(1, 1, 1, 0.5f));
			tex2.Apply();

			tex3 = new Texture2D(1, 1);
			tex3.SetPixel(0, 0, new Color(0.7f, 0.7f, 0.7f, 1f));
			tex3.Apply();

			skin = Object.Instantiate(GUI.skin);
			var styles = new GUIStyle[] { skin.verticalScrollbar, skin.button };
			foreach (var s in styles)
			{
				s.normal.background = tex;
				s.hover.background = tex3;
				s.active.background = tex3;
				s.focused.background = tex3;
			}

			var styles2 = new GUIStyle[] { skin.verticalScrollbarThumb, skin.verticalSliderThumb };
			foreach (var s in styles2)
			{
				s.normal.background = tex2;
				s.hover.background = tex2;
				s.active.background = tex2;
				s.focused.background = tex2;
			}
			skin.window.normal.background = tex2;
			skin.button.margin = new RectOffset(0, 0, 0, 0);
			//skin.button.fixedHeight = 40;
			skin.button.fontSize = 16;
			skin.button.padding.top = 8;
			skin.button.padding.bottom = 8;
			toleft = new GUIStyle(skin.button);
			toleft.alignment = TextAnchor.MiddleLeft;
		}
		GUI.skin = skin;

		GUILayout.BeginArea(new Rect(Screen.width * 0.25f, Screen.height * 0.25f, Screen.width * 0.5f, Screen.height * 0.5f));
		GUILayout.FlexibleSpace();
		GUILayout.Label(scene + " game", skin.button);
		GUILayout.Space(2);
		if (ask != null)
		{
			GUILayout.Label(ask, skin.button);
			GUILayout.Space(2);
			GUILayout.BeginHorizontal();
			if (GUILayout.Button("Yes"))
			{
				doSave(maybesave);
			}
			if (GUILayout.Button("No"))
			{
				maybesave = null;
				ask = null;
			}
			GUILayout.EndHorizontal();
		}
		else
		{
			scrollpos = GUILayout.BeginScrollView(scrollpos);
			if (scene == "Save" && GUILayout.Button("<new save slot>", toleft))
				doSave();
			foreach (var b in saves)
			{
				var bn = b.Value.ToString(datefmt) + " - " + Path.GetFileNameWithoutExtension(b.Key);
				if (GUILayout.Button(bn, toleft))
				{
					if (scene == "Load")
					{
						scene = null;
						enableRaycasts();
						ScriptEnv.game.saveData.LoadFull(b.Key);
						//ScriptEnv.game.saveData.player.ext.changeMap = false;
						//Singleton<Game>.Instance.saveData.isOpening = false;
						if (!ScriptEnv.scene.NowSceneNames.Contains("NightMenu"))
						{
							print("loading from title");
							ScriptEnv.scene.LoadReserve(new Scene.Data { levelName = "Action", fadeType = FadeType.None }, false);
						} else
						{
							print("loading from home");
							Object.FindObjectOfType<NightMenuScene>().onLoadSubject.OnNext(Unit.Default);
							//if (Singleton<Game>.Instance.saveData.dayPeriod != (int)Cycle.Type.MyHouse)
							//	ScriptEnv.scene.UnLoad();
						}
					}
					else
					{
						ask = $"Overwrite {bn}?";
						maybesave = b.Key;
					}
				}
			}
			GUILayout.EndScrollView();
			GUILayout.Space(2);
			if (GUILayout.Button("Close"))
			{
				enableRaycasts();
			}
		}
		GUILayout.FlexibleSpace();
		GUILayout.EndArea();
	}
	public void doSave(string path = null)
	{
		print("Saving game");
		if (path == null)
		{
			int idx = 0;
			while (File.Exists(path = savedir + "/" + ScriptEnv.game.Player.Name.Trim() + " at " + ScriptEnv.game.saveData.accademyName.Trim() + ((idx == 0) ? "" : idx.ToString()) + ".dat"))
				idx++;
		}

		//Singleton<Game>.Instance.saveData.dayPeriod = (int)ScriptEnv.act.Cycle.nowType;
		//Singleton<Game>.Instance.saveData.player.ext.changeMap = true;
		ScriptEnv.game.saveData.SaveFull(path, true);
		enableRaycasts();
		print("Saved");
	}
}