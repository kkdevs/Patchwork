using Patchwork;
using UnityEngine;

public class GhettoUI : ScriptEvents
{
	public static GUISkin skin;
	public static GUIStyle toleft;
	public static GUIStyle largebutton;
}

public class GhettoScript : ScriptEvents
{
	[Prio(9000)]
	public override void OnGUI()
	{
		if (GhettoUI.skin == null)
		{
			GUISkin skin;
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

			var largebutton = new GUIStyle(skin.button);
			largebutton.fontSize = 32;
			largebutton.wordWrap = true;

			var toleft = new GUIStyle(skin.button);
			toleft.alignment = TextAnchor.MiddleLeft;
			GhettoUI.skin = skin;
			GhettoUI.toleft = toleft;
			GhettoUI.largebutton = largebutton;
		}
		GUI.skin = GhettoUI.skin;
	}
}
