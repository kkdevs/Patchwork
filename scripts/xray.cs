//@INFO: XRay experiment
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

		// sound file activating this entry
		[System.NonSerialized]
		public Texture2D[][] frames;
	}
	public List<RayInfo> infos = new List<RayInfo>();
	public override void Start()
	{
		foreach (var dir in Directory.GetDirectories(Dir.pw + "xray"))
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
		var id = (int)ha?.flags.selectedAnimationListInfo?.id;
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
		var ai = hse.lstInfo[animkey];
		var key = ai.key[soundkey];
		var id = (int)hse.flags.selectedAnimationListInfo?.id;
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

#if false
	public Texture2D[] texes;
	public override void OnHPlay(HActionBase ha, string anim)
	{
		if (ha.flags && ha.flags.selectedAnimationListInfo != null)
			print("Selected anim " + ha.flags.selectedAnimationListInfo.id);
	}
	public override void OnHSound(HSeCtrl hse, int animkey, int soundkey)
	{
		var ai = hse.lstInfo[animkey];
		var key = ai.key[soundkey];
		if (hse.flags?.selectedAnimationListInfo != null)
		{
			print("H sound for " + hse.flags.selectedAnimationListInfo.id);
		} 
		print("hsound key "+ai.nameAnimation + " " + key.nameSE + " " + key.isLoop);
		if (key.nameSE == "khse_06")
		{
			xray = 0;
			t = 0;
			expires = 1f;
			loop = 3;
			keydelta = 1.5f / texes.Length / loop;
		}
	}
	public Material mat;
	public int tick;
	public float t;
	public int loop;
	public float keydelta;
	public float expires;

	public override void Update()
	{
		if (xray < 0)
			return;
		t += Time.deltaTime;
		if (t > keydelta)
		{
			if (xray < texes.Length - 1)
			{
				t -= keydelta;
				xray++;
			}
			else if (loop > 1)
			{
				xray = 0;
				loop--;
			}
		}
		if (t > expires)
			xray = -1;
	}
	public int xray = -1;
	public override void OnGUI()
	{
		/*if (Event.current.type == EventType.KeyDown)
		{
			if (Event.current.keyCode == KeyCode.KeypadMinus)
				keydelta
		}*/
		if (!Event.current.type.Equals(EventType.Repaint))
			return;
		if (xray < 0)
			return;
		int pos = (tick / 4) % texes.Length;
		var g = new Color(1.5f, 1.5f, 1.5f);
		var h = Screen.height / 4;
		var tw = texes[pos].width;
		var th = texes[pos].height;
		Graphics.DrawTexture(new Rect(0, Screen.height/4, tw * h / th, h), texes[xray]);
	}
#endif
}
