// prettify this, someday

using ParadoxNotion.Serialization.FullSerializer;
using static Patchwork;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

public class GhettoConfig
{
	bool locked;
	string cfpath => Dir.pw + GetType().Name + ".json";
	static bool loading;

	public GhettoConfig()
	{
		if (loading) return;
		if (!Load())
			Save();
	}
	public bool Load()
	{
		if (loading)
			return true;
		if (File.Exists(cfpath))
		{
			loading = true;
			var self = JSON.Deserialize(Encoding.UTF8.GetString(File.ReadAllBytes(cfpath)), this);
			loading = false;
			if (self != this)
			{
				Script.print($"WARNING: Failed to load {GetType().Name}.json, probably because of field type mismatch.");
				return false;
			}
			return true;
		}
		return false;
	}
	public void Save()
	{
		loading = true;
		fsGlobalConfig.SerializeDefaultValues = true;
		File.WriteAllBytes(cfpath, JSON.Serialize(this, true).ToBytes());
		fsGlobalConfig.SerializeDefaultValues = false;
		loading = false;
	}

}

public static class GhettoSkin
{
}

public class GhettoUI : ScriptEvents
{
	public static Dictionary<string, Event> scuts = new Dictionary<string, Event>();
	public static bool IsKey(string scut)
	{
		if (!scuts.TryGetValue(scut, out Event ev))
		{
			ev = Event.KeyboardEvent(scut);
			ev.type = EventType.KeyDown;
			scuts[scut] = ev;
		}
		return ev.Equals(Event.current);
	}
	public static object Config(Type t, bool load = true)
	{
		fsSerializer json = new fsSerializer();
		var owner = t.Name;
		var cfn = Dir.pw + owner + ".json";
		fsData data;
		var obj = Activator.CreateInstance(t);
		if (File.Exists(cfn))
		{
			data = fsJsonParser.Parse(Encoding.UTF8.GetString(File.ReadAllBytes(cfn)));
			json.TryDeserialize(data, ref obj);
			return obj;
		}
		fsGlobalConfig.SerializeDefaultValues = true;
		json.TrySerialize(obj, out data);
		File.WriteAllBytes(cfn, fsJsonPrinter.PrettyJson(data).ToBytes());
		return obj;
	}


	public static GUISkin skin;
	public static GUIStyle toleft;
	public static GUIStyle largebutton;
	public static GUIStyle selected;
	public static GUIStyle unselected;
	public static void BuildSkin()
	{
		print("Building ghetto skin");
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

		skin = UnityEngine.Object.Instantiate(GUI.skin);
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
		skin.button.fontSize = 16;
		skin.button.padding.top = 8;
		skin.button.padding.bottom = 8;
		skin.button.padding.left = 0;
		skin.button.padding.right = 0;


		largebutton = new GUIStyle(skin.button);
		largebutton.fontSize = 32;
		largebutton.wordWrap = true;

		selected = new GUIStyle(skin.button);
		selected.normal.background = selected.active.background;

		unselected = new GUIStyle(skin.button);
		selected.margin = new RectOffset(1, 1, 1, 1);
		unselected.margin = new RectOffset(1, 1, 1, 1);

		toleft = new GUIStyle(skin.button);
		toleft.alignment = TextAnchor.MiddleLeft;
	}
	public static void UpdateSkin()
	{
		selected.fontSize = unselected.fontSize = skin.button.fontSize = Screen.height / 42;
		largebutton.fontSize = Screen.height / 18;
	}

}

public class GhettoScript : ScriptEvents
{
	[Prio(9000)]
	public override void OnGUI()
	{
		if (GhettoUI.skin == null)
			GhettoUI.BuildSkin();
		GhettoUI.UpdateSkin();
		GUI.skin = GhettoUI.skin;
	}
	
}
