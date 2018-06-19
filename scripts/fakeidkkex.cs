//@INFO: Support for loading cards with bepinex ExtendedSave
//@DESC: UAR<->fakeid interop
//@DEP: kkex fakeid

using System;
using System.IO;
using System.Reflection;
using UnityEngine;
using Patchwork;
using System.Linq;
using System.Collections.Generic;
using MessagePack;
using static ChaListDefine;

public class FakeIDKKEx : MonoBehaviour
{
	[MessagePackObject(true)]
	public class ResolveInfo
	{
		public string ModID;
		public int Slot;
		public string Property;
		public int Category;
	}

	public void Awake()
	{
		foreach (var kv in propName2Path)
			path2PropName[kv.Value] = kv.Key;
	}

	public void OnCardLoad_900(ChaFile f, BlockHeader bh, bool nopng, bool nostatus)
	{
		try
		{
			TryImport(f.dict.Get<KKEx>("kkex"), f.dict.Get<FakeID.GuidMap>("guidmap"));
		}
		catch { };
	}

	public void OnCardSave(ChaFile f, BinaryWriter w, List<object> blocks, bool nopng)
	{
		var map = f.dict.Get<FakeID.GuidMap>("guidmap");
		var objlist = new object[map.items.Count];
		int idx = 0;
		foreach (var entry in map.items)
		{
			objlist[idx++] = MessagePackSerializer.Serialize(new ResolveInfo()
			{
				ModID = entry.Value.guid,
				Property = path2PropName[entry.Value.prop],
				Slot = entry.Value.id,
				Category = entry.Value.cat,
			});
		}
		var kkex = f.dict.Get<KKEx>("kkex");
		var kv = new Dictionary<string, object>();
		kv["info"] = objlist;
		var tmp = kkex.data["com.bepis.sideloader.universalautoresolver"] = new KeyValuePair<int, Dictionary<string, object>>(0, kv);
	}

	public void TryImport(KKEx k, FakeID.GuidMap map) {
		var guids = k.data["com.bepis.sideloader.universalautoresolver"].Value["info"] as object[];
		foreach (var entry in guids)
		{
			var e = MessagePackSerializer.Deserialize<ResolveInfo>(entry as byte[]);
			var prop = e.Property;
			if (!propName2Path.TryGetValue(prop, out prop))
			{
				print($"Failed to recover property path for {e.Property}");
				continue;
			}
			if (!map.items.ContainsKey(prop)) map.items[prop] = new FakeID.GuidMap.Item()
			{
				cat = -1,
				id = e.Slot,
				prop = prop,
				guid = e.ModID
			};
		}
	}

	// The UAR format is not reflection aware and all rewrites would need to be done "by hand" (fragile).
	// Instead, we translate those records back into reflection-readable format suitable for fakeid rewriter.
	public static Dictionary<string, string> path2PropName = new Dictionary<string, string>();
	public static Dictionary<string, string> propName2Path = new Dictionary<string, string>()
	{
/*		{ "cheekId", CategoryNo.mt_cheek},
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
		{ "whiteId", CategoryNo.mt_eye_white},*/
	};
}
