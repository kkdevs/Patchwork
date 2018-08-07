//@INFO: Animated H xray
//@VER: 1

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

public class XRay : ScriptEvents
{
	public class RayInfo
	{
		public bool animsync = false;
		public float timescale = 1;
		public List<string> anims = new List<string>();
		public Dictionary<string, float> modifier = new Dictionary<string, float>();
		public string sound;
		// h positions this applies for
		public List<int> positions = new List<int>();
		// how many times the clip should be looped through for each sound
		public int loop = 1;
		// how long is the clip, in seconds
		public float length = 1;
		// after how many seconds we hide the clip
		public float expires = 1;

		[System.NonSerialized]
		public Texture2D[][] frames;
	}
	public List<RayInfo> infos = new List<RayInfo>();
	public override void Start()
	{
		var xdir = Dir.pw + "xray";
		if (!Directory.Exists(xdir))
		{
			print("No XRay animation clips found; disabling.");
			return;
		}
		foreach (var dir in Directory.GetDirectories(xdir))
		{
			var info = dir + "/info.json";
			if (!File.Exists(info)) continue;
			var entries = JSON.Deserialize<List<RayInfo>>(File.ReadAllText(info));
			infos.AddRange(entries);
			var tmp = new List<Texture2D[]>();
			foreach (var texdir in Directory.GetDirectories(dir))
			{
				var tlist = new List<Texture2D>();
				foreach (var png in Directory.GetFiles(texdir, "*.png"))
				{
					spam("Adding " + png);
					var tex = new Texture2D(2, 2);
					tex.LoadImage(File.ReadAllBytes(png));
					tlist.Add(tex);
				}
				tmp.Add(tlist.ToArray());
			}
			foreach (var entry in entries)
				entry.frames = tmp.ToArray();
			print("Loaded " + info + " with " + tmp.Count + " sequences");
		}
	}

	public RayInfo current;
	public int index;
	public int subindex;
	public float timeout;
	public float keydelta;
	public float t;
	public int loop;
	HActionBase hab;

	public override void OnHPlay(HActionBase ha, string anim)
	{
		var sel = ha?.flags?.selectedAnimationListInfo;
		if (sel == null)
			return;
		var id = sel.id;
		spam("Setting up position " + id + anim);
		var entry = infos.Find(x => x.animsync && x.positions.Contains(id) && x.anims.Contains(anim));
		if (entry == null)
		{
			current = null;
			hab = null;
			spam("no entry");
			return;
		}
		index = 0;
		current = entry;
		hab = ha;
	}

	public override void OnHSound(HSeCtrl hse, int animkey, int soundkey)
	{
		var sel = hse?.flags?.selectedAnimationListInfo;
		if (sel == null)
			return;
		var id = sel.id;

		var ai = hse.lstInfo[animkey];
		var key = ai.key[soundkey];
		spam("H sound "+key.nameSE+"for " + id);

		var entry = infos.Find(x=>x.animsync == false && x.positions.Contains(id) && x.sound == key.nameSE);
		if (entry == null)
			return;
		// same sound - try to advance index to next sequence
		if (entry == current && index < entry.frames.Length - 1)
			index++;
		if (entry == null)
			index = 0;
		current = entry;
		subindex = 0;
		timeout = entry.expires;
		keydelta = entry.length / current.frames[index].Length / entry.loop;
		loop = entry.loop;
		t = 0;
	}

	public void UpdateSoundSync()
	{
		t += Time.deltaTime;
		if (t > keydelta)
		{
			// move onto next frame
			if (subindex < current.frames[index].Length - 1)
			{
				t -= keydelta;
				subindex++;
			} else
			// have we reached end of sequence?
			{
				// but we can restart the loop
				if (loop > 1)
				{
					t = 0;
					loop--;
					subindex = 0;
				}
				// otherwise keep it frozen on last frame
				else subindex = current.frames[index].Length - 1;
			}
		}
		if (t > timeout)
			current = null;
	}

	public void UpdateAnimSync()
	{
		var ai = hab.female.getAnimatorStateInfo(0);
		subindex = (int)((((ai.normalizedTime % 1f) * current.timescale)) * current.frames[0].Length);
		if (subindex >= current.frames[0].Length)
			subindex = current.frames[0].Length - 1;
	}

	public override void Update()
	{
		if (current == null)
			return;
		if (current.animsync)
			UpdateAnimSync();
		else
			UpdateSoundSync();
	}

	public override void OnGUI()
	{
		if (!Event.current.type.Equals(EventType.Repaint))
			return;
		if (current == null)
			return;
		var h = Screen.height / 4;
		var tex = current.frames[index][subindex];
		var tw = tex.width;
		var th = tex.height;
		Graphics.DrawTexture(new Rect(0, Screen.height / 4, tw * h / th, h), tex);
	}

}
