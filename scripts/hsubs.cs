//@INFO: Display subs in h
//@VER: 1

using Patchwork;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using MessagePack;

public class HSubs : GhettoUI
{
	// change this if you want private tl, replace the '1U0pRyY8e2fIg0E4iBXXRIzpGGDBs5W_g9KfjObS-xI0' part with your shareable-link
	// doc-id (must be anyone-can-view at least)
	public static string sheet = "https://docs.google.com/spreadsheets/d/1U0pRyY8e2fIg0E4iBXXRIzpGGDBs5W_g9KfjObS-xI0/export?format=csv";
	// public stsatic string sheet = null; // disable translation


	public static HSubs instance;
	public override void Start()
	{
		instance = this;
		UpdateSubs();
	}

	public static void UpdateSubs()
	{
		if (sheet != null)
			instance.StartCoroutine(instance.DownloadSubs());
	}

	public Dictionary<string, string> dict = new Dictionary<string, string>();

	public IEnumerator DownloadSubs()
	{
		var cache = Program.tempbase + "hsubs.msgpack";
		if (File.Exists(cache))
		{
			dict = LZ4MessagePackSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllBytes(cache));
			print("Found cached hsubs");
		}

		print("Downloading subs from " + sheet);
		var dl = new WWW(sheet);
		while (!dl.isDone)
			yield return dl;
		print($"Parsing {dl.text.Length} bytes");
		int cnt = 0;
		foreach (var row in CSV.ParseCSV(dl.text))
		{
			int idx = 0;
			string sound = null;
			string tl = null;
			foreach (var cell in row)
			{
				if (idx == 0)
					sound = cell.ToLower();
				if (idx == 2)
					tl = cell;
				idx++;
			}
			if (sound != null && tl != null && sound.Length < 64)
			{
				cnt++;
				dict[sound] = tl;
			}
		}
		print($"Done. {cnt} lines found.");
		if (cnt > 60000)
			File.WriteAllBytes(cache, LZ4MessagePackSerializer.Serialize(dict));
	}

	public string currentLine;
	public float expires;
	public override void OnPlayVoice(LoadVoice v)
	{
		if (v.word == null)
			return;
		var audioSource = v.audioSource;
		if (audioSource == null || v.audioSource.loop)
			return;
		expires = Time.realtimeSinceStartup + audioSource.clip.length - audioSource.time;
		if (!dict.TryGetValue(v.assetName.ToLower(), out currentLine))
			currentLine = v.word;
		if (Program.settings.enableSpam)
			print($"[HSUBS] [{v.assetName}] '{v.word}' => '{currentLine}'");
		GUIUtility.systemCopyBuffer = v.word;
	}
	public bool hasUI;

	public override void Occasion()
	{
		var can = GameObject.Find("Canvas")?.GetComponent<Canvas>();
		hasUI = can == null ? false : can.enabled;
	}
	public override void OnGUI()
	{
		if (!hasUI || currentLine.IsNullOrEmpty() || Time.realtimeSinceStartup > expires) {
			currentLine = null;
			return;
		}
		GUILayout.BeginArea(new Rect(Screen.width * 0.1f, 0, Screen.width * 0.8f, Screen.height * 0.9f));
		GUILayout.FlexibleSpace();
		GUILayout.Label(currentLine, largebutton);
		GUILayout.EndArea();
	}
}

// repl command
public partial class ScriptEnv
{
	//public static void UpdateSubs() => HSubs.UpdateSubs();
}
