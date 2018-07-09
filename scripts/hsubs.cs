//@INFO: Display subs in h
//@VER: 2

using Patchwork;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using MessagePack;

public class HSubsConfig : GhettoConfig
{
	public string sheet = "https://docs.google.com/spreadsheets/d/1U0pRyY8e2fIg0E4iBXXRIzpGGDBs5W_g9KfjObS-xI0";
	public string gid = "677855862";
	public string range = "A1:C";
	//public string editsheet = "/edit#gid=677855862&range="; // identifies the tab
	public bool copyToClipboard = false;
	public bool copyEditUrl = false;
	public bool copyJPLine = false;
	public string openURLshortcut = "^G";
	public string reloadshortcut = "^R";
	public string cycleShortcut = "^N";
	public bool updateOnStart = true;

	// negative number is percent of screen height
	public float fontSize = -5;

	// canvas renderer specific
	public bool useCanvasRenderer = true;
	public string fontName = "Arial.ttf";
	public Vector2 outlineThickness = new Vector2(2, 2);
	public TextAnchor textAlignment = TextAnchor.LowerCenter;
	public FontStyle fontStyle = FontStyle.Bold;
	public Color outlineColor = new Color(0, 0, 0, 1);
	public Color textColor = new Color(1, 1, 1, 1);
	public float textOffset = 24;
}

public class HSubs : GhettoUI
{
	public static HSubsConfig cfg = new HSubsConfig();
	public Dictionary<string, KeyValuePair<int,string>> dict = new Dictionary<string, KeyValuePair<int,string>>();
	public string currentLine;
	public string currentJPLine;
	public LoadVoice currentVoice;
	public string editrow;
	public float expires;
	public bool hasUI;
	public bool showJP;
	public string downloading;
	public static HSubs instance;

	public int fontSize => (int)(cfg.fontSize < 0 ? ((cfg.fontSize * Screen.height / -100.0)) : cfg.fontSize);

	public override void Start()
	{
		instance = this;
		if (cfg.updateOnStart)
			UpdateSubs();
	}

	public static bool UpdateSubs()
	{
		if (!cfg.sheet.IsNullOrEmpty())
		{
			instance.StartCoroutine(instance.DownloadSubs());
			return true;
		}
		return false;
	}

	public bool dlpending;
	public IEnumerator DownloadSubs()
	{
		if (dlpending) yield break;
		var cache = Program.tempbase + "hsubs2.msgpack";
		if (File.Exists(cache))
		{
			dict = LZ4MessagePackSerializer.Deserialize<Dictionary<string, KeyValuePair<int,string>>>(File.ReadAllBytes(cache));
			print("Found cached hsubs");
		}

		var furl = cfg.sheet + "/export?exportFormat=csv&gid=" + cfg.gid + "&range=" + cfg.range;
		print("Downloading subs from " + furl);
		var dl = new WWW(furl);
		while (!dl.isDone)
			yield return dl;
		if (dl.error != null)
		{
			if (downloading != null)
			{
				downloading = "Failed: " + dl.error;
				yield return new WaitForSeconds(5);
			} else
			{
				print(dl.error);
			}
			downloading = null;
			dlpending = false;
			yield break;
		}
		print($"Parsing {dl.text.Length} characters");
		int cnt = 0;
		int nrow = 0;
		foreach (var row in CSV.ParseCSV(dl.text))
		{
			nrow++;
			string en = null;
			int idx = 0;
			string sound = null;
			foreach (var cell in row)
			{
				if (idx == 0)
					sound = cell.ToLower();
				if (idx == 2)
					en = cell;
				idx++;
			}
			if (sound != null && en != null && sound.Length < 64)
			{
				cnt++;
				dict[sound] = new KeyValuePair<int, string>(nrow, en);
			}
		}
		print($"Done. {cnt} lines found.");
		if (cnt > 60000)
			File.WriteAllBytes(cache, LZ4MessagePackSerializer.Serialize(dict));
		dlpending = false;
		downloading = null;
	}


	public override void OnPlayVoice(LoadVoice v)
	{
		if (v.word == null)
			return;
		var audioSource = v.audioSource;
		if (audioSource == null || v.audioSource.loop)
			return;
		currentVoice = v;
		expires = Time.realtimeSinceStartup + audioSource.clip.length / Mathf.Abs(audioSource.pitch);
		KeyValuePair<int, string> currentPair = new KeyValuePair<int, string>(-1, v.word);
		dict.TryGetValue(v.assetName.ToLower(), out currentPair);
		if (Program.settings.enableSpam)
			print($"[HSUBS] [{v.assetName}] '{v.word}' => '{currentPair.Value}'");
		if (!cfg.sheet.IsNullOrEmpty() && currentPair.Key >= 0)
			editrow = cfg.sheet + "/edit#gid="+cfg.gid+"&range=C" + currentPair.Key;
		if (cfg.copyToClipboard)
		{
			var buf = "";
			if (cfg.copyJPLine)
				buf += v.word + "\n";
			if (cfg.copyEditUrl)
				buf += editrow;
			GUIUtility.systemCopyBuffer = buf;
		}
		currentLine = currentPair.Value;
		currentJPLine = v.word;
	}

	public override void Occasion()
	{
		var can = GameObject.Find("Canvas")?.GetComponent<Canvas>();
		hasUI = can == null ? false : can.enabled;
	}

	public GUIStyle substyle;

	public override void OnGUI()
	{
		if (IsKey(cfg.cycleShortcut))
			showJP = !showJP;
		if (IsKey(cfg.reloadshortcut))
		{
			try
			{
				cfg.Load();
			} catch (System.Exception ex) { print(ex.ToString()); }
			Object.Destroy(panel);
			panel = null;
			if (UpdateSubs())
				downloading = "Updating subs...";
		}

		if (downloading == null && !currentLine.IsNullOrEmpty())
		{
			if (expires < Time.realtimeSinceStartup)
			{
				if (currentVoice != null && currentVoice.audioSource != null && !currentVoice.audioSource.isPlaying)
				{
					currentLine = null;
					currentJPLine = null;
				}
			}
		}

		if (substyle == null)
		{
			substyle = new GUIStyle(GUI.skin.button);
			substyle.wordWrap = true;
		}
		substyle.fontSize = fontSize;
		var ev = Event.current;
		if (IsKey(cfg.openURLshortcut))
			System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(editrow));

		var text = downloading ?? (showJP ? currentJPLine : currentLine);
		if (!cfg.useCanvasRenderer)
		{
			GUILayout.BeginArea(new Rect(Screen.width * 0.1f, 0, Screen.width * 0.8f, Screen.height * 0.9f));
			GUILayout.FlexibleSpace();
			GUILayout.Label(text, substyle);
			GUILayout.EndArea();
		}
		else if (lastCanvasLine != text)
		{
			MakeCanvas();
			lastCanvasLine = text;
			subtitleText.text = text ?? "";
		}
	}

	public string lastCanvasLine;
	public Text subtitleText;
	public GameObject panel;
	public void MakeCanvas()
	{
		if (!cfg.useCanvasRenderer) return;
		if (panel == null)
			panel = GameObject.Find("HSubs");
		else
			if (subtitleText != null) return;
		if (panel != null)
		{
			subtitleText = panel.GetComponent<Text>();
			if (subtitleText != null)
				return;
			Object.Destroy(panel);
		}

		panel = new GameObject("HSubs");
		var canvas = panel.AddComponent<Canvas>();

		canvas.renderMode = RenderMode.ScreenSpaceOverlay;
		canvas.overrideSorting = true;
		canvas.sortingOrder = 1;
		var content = new GameObject("HSubsContent");
		content.transform.SetParent(panel.transform, true);
		var rect = content.AddComponent<RectTransform>();
		rect.sizeDelta = new Vector2(0f, 0f);
		rect.anchorMin = new Vector2(0f, 0f);
		rect.anchorMax = new Vector2(1f, 1f);
		rect.pivot = new Vector2(0, 0);
		var outline = content.AddComponent<Outline>();
		subtitleText = content.AddComponent<Text>();
		subtitleText.transform.SetParent(content.transform, false);
		var myFont = (Font)Resources.GetBuiltinResource(typeof(Font), cfg.fontName);
		subtitleText.font = myFont;
		subtitleText.material = myFont.material;
		outline.effectDistance = cfg.outlineThickness;
		outline.effectColor = cfg.outlineColor;
		subtitleText.fontSize = fontSize;
		subtitleText.fontStyle = cfg.fontStyle;
		subtitleText.material.color = cfg.textColor;
		subtitleText.alignment = cfg.textAlignment;
		subtitleText.rectTransform.anchoredPosition = new Vector2(0, cfg.textOffset);
	}
}
