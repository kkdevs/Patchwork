// compatibility for bepinex

using System;
using System.IO;
using System.Reflection;
using UnityEngine;
using Patchwork;
using System.Linq;
using System.Collections.Generic;
using MessagePack;
using static ChaListDefine;

public class fixplugins : MonoBehaviour
{
	public static GameObject bgo => GameObject.Find("BepInEx_Manager");
	public static bool hasBepinGo => bgo != null;
	public static bool hasBepinAss =>
		AppDomain.CurrentDomain.GetAssemblies().First(x => x.FullName.ToLower().StartsWith("bepinex")) != null;

	// bepinex can crash with us for multitude reasons. try to resuscite it, or
	// at least the plugins.
	void Awake()
	{
		// its already in appdomain, it probably loaded correctly
		if (hasBepinGo)
		{
			print("BepinEx already loaded and (probably) running ok.");
			fixFilters();
			return;
		}
		print("Trying to fix plugins");
		// it broke, so load all plugins "manually"
		string path = Path.GetFullPath(Application.dataPath + "/../bepinex");
		try
		{
			foreach (var dll in Directory.GetFiles(path + "/core", "*.dll"))
			{
				try
				{
					Assembly.Load(AssemblyName.GetAssemblyName(dll));
				}
				catch { };
			}
		}
		catch {};

		if (!hasBepinAss)
			try {
				Assembly.Load(Application.dataPath + "/Managed/bepinex.dll");
			} catch {
				// none found, bail
				return;
			};

		foreach (var dll in Directory.GetFiles(path, "*.dll"))
		{
			try
			{
				var an = AssemblyName.GetAssemblyName(dll);
				var ass = AppDomain.CurrentDomain.Load(an);
				foreach (var t in ass.GetTypesSafe())
					if (t.BaseType?.Name == "BaseUnityPlugin")
						gameObject.AddComponent(t);
			}
			catch (Exception ex)
			{
				print(ex);
			};
		}

	}

	// since we have different exe name, all filters based on that are
	// bogus.
	public void fixFilters()
	{
		foreach (var ass in AppDomain.CurrentDomain.GetAssemblies())
			foreach (var t in ass.GetTypesSafe())
				if (t.BaseType?.Name == "BaseUnityPlugin")
				{
					try
					{
						if (t.GetCustomAttributes(false).First(x => x.GetType().Name == "BepInProcess") != null)
							if (gameObject.GetComponent(t) == null)
							{
								print("Instancing filtered plugin " + t.Name);
								gameObject.AddComponent(t);
							}
					}
					catch { };
				}
	}

	// describe the BlockHeader
	public class kkex
	{
		public static string Version => "3";
		public static string BlockName = "KKEx";
		public Dictionary<string, KeyValuePair<int, Dictionary<string, object>>> data;
		public byte[] SaveBytes() => MessagePackSerializer.Serialize(data);
		public void LoadBytes(byte[] buf, Version ver)
		{
			data = MessagePackSerializer.Deserialize<Dictionary<string, KeyValuePair<int, Dictionary<string, object>>>>(buf);
		}
	}

	// one slot mapping stored by sideloader
	[MessagePackObject(true)]
	public class ResolveInfo
	{
		public string ModID;
		public int LocalSlot;
		public string Property;
	}

	// import sideloader slot mappings and turn those into guid preference
	// per <cat,id> pair.
	public void RegisterBepinGuids(ChaFile f, kkex k)
	{
		var guids = k.data["com.bepis.sideloader.universalautoresolver"].Value["info"] as object[];
		foreach (var entry in guids)
		{
			var e = MessagePackSerializer.Deserialize<ResolveInfo>(entry as byte[]);
			var prop = e.Property;
			var prop2 = prop.Split('.').Last();
			CategoryNo catno;
			if (!getcat.TryGetValue(prop, out catno) && !getcat.TryGetValue(prop2, out catno))
			{
				print($"Unrecognized bepinex category mapping {e.Property}");
				continue;
			}
			var prefs = f.dict.guidPrefs;
			var pair = new KeyValuePair<int,int>((int)catno, e.LocalSlot);
			List<string> chain;
			if (!prefs.TryGetValue(pair, out chain))
				prefs[pair] = chain = new List<string>();
			if (!chain.Contains(e.ModID))
				chain.Add(e.ModID);
		}
	}

	public void OnCardLoad(ChaFile f, BlockHeader bh, bool nopng, bool nostatus)
	{
		print("Loading card");
		if (nopng) return;
		var k = new kkex();
		if (bh.Load(k))
		{
			// remember kkex so that we can save it back
			f.dict.dict["kkex"] = k;
			try
			{
				RegisterBepinGuids(f, k);
			}
			catch (Exception ex) { print(ex); };
		}
	}

	// when card is saved (to a file, not savegame), dump back any bepinex
	// info we've learned earlier into it
	public void OnCardSave(ChaFile f, BinaryWriter w, List<object> blocks, bool nopng)
	{
		if (nopng) return;
		var data = f.dict.Get<kkex>("kkex");
		if (data != null)
			blocks.Add(data);
	}

	// deobfuscate category
	static Dictionary<string, CategoryNo> getcat = new Dictionary<string, CategoryNo>()
	{
			{ "cheekId", CategoryNo.mt_cheek},
			{ "ClothesBot", CategoryNo.co_bot},
			{ "ClothesBra", CategoryNo.co_bra},
			{ "ClothesGloves", CategoryNo.co_gloves},
			{ "ClothesJacketSubA", CategoryNo.cpo_jacket_a},
			{ "ClothesJacketSubB", CategoryNo.cpo_jacket_b},
			{ "ClothesJacketSubC", CategoryNo.cpo_jacket_c},
			{ "ClothesPants", CategoryNo.co_panst},
			{ "ClothesSailorSubA", CategoryNo.cpo_sailor_a},
			{ "ClothesSailorSubB", CategoryNo.cpo_sailor_b},
			{ "ClothesSailorSubC", CategoryNo.cpo_sailor_c},
			{ "ClothesShoesInner", CategoryNo.co_shoes},
			{ "ClothesShoesOuter", CategoryNo.co_shoes},
			{ "ClothesShorts", CategoryNo.co_shorts},
			{ "ClothesSocks", CategoryNo.co_socks},
			{ "ClothesTop", CategoryNo.co_top},
			{ "ChaFileBody.detailId", CategoryNo.mt_body_detail},
			{ "ChaFileFace.detailId", CategoryNo.mt_face_detail},
			{ "eyebrowId", CategoryNo.mt_eyebrow},
			{ "eyelineDownId", CategoryNo.mt_eyeline_down},
			{ "eyelineUpId", CategoryNo.mt_eyeline_up},
			{ "eyeshadowId", CategoryNo.mt_eyeshadow},
			{ "glossId", CategoryNo.mt_hairgloss},
			{ "HairBack", CategoryNo.bo_hair_b},
			{ "HairFront", CategoryNo.bo_hair_f},
			{ "HairOption", CategoryNo.bo_hair_o},
			{ "HairSide", CategoryNo.bo_hair_s},
			{ "headId", CategoryNo.bo_head},
			{ "hlDownId", CategoryNo.mt_eye_hi_down},
			{ "hlUpId", CategoryNo.mt_eye_hi_up},
			{ "lipId", CategoryNo.mt_lip},
			{ "lipLineId", CategoryNo.mt_lipline},
			{ "moleId", CategoryNo.mt_mole},
			{ "nipId", CategoryNo.mt_nip},
			{ "noseId", CategoryNo.mt_nose},
			{ "ChaFileBody.PaintID1", CategoryNo.mt_body_paint},
			{ "ChaFileFace.PaintID1", CategoryNo.mt_face_paint},
			{ "ChaFileBody.PaintID2", CategoryNo.mt_body_paint},
			{ "ChaFileFace.PaintID2", CategoryNo.mt_face_paint},
			{ "PaintLayoutID1", CategoryNo.bodypaint_layout},
			{ "PaintLayoutID2", CategoryNo.bodypaint_layout},
			{ "Pupil1", CategoryNo.mt_eye},
			{ "Pupil2", CategoryNo.mt_eye},
			{ "sunburnId", CategoryNo.mt_sunburn},
			{ "underhairId", CategoryNo.mt_underhair},
			{ "whiteId", CategoryNo.mt_eye_white},
	};
}

