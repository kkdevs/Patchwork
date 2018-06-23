//@INFO: Support for loading cards with bepinex ExtendedSave
//@DESC: UAR<->fakeid interop
//@DEP: kkex fakeid
//@VER: 1

using UnityEngine;
using MessagePack;
using Patchwork;

public class FakeIDKKEx : ScriptEvents
{
	// bepinex retains no structure suitable for reflection/eval() based rewriting.
	// fuzzy reconstruct the property path to a (hopefuly) valid C# expression
	public string[] tab = new string[] {
		"PaintID1", "paintId[0]",
		"PaintID2", "paintId[1]",
		"PaintLayoutID1", "paintLayoutId[0]",
		"PaintLayoutID2", "paintLayoutId[1]",
		"Pupil1", "pupil[0].id",
		"Pupil2", "pupil[1].id",

		"HairBack", "parts[0].id",
		"HairFront", "parts[1].id",
		"HairSide", "parts[2].id",
		"HairOption", "parts[3].id",

		"ClothesTop", "parts[0].id",
		"ClothesBot", "parts[1].id",
		"ClothesBra", "parts[2].id",
		"ClothesShorts", "parts[3].id",
		"ClothesGloves", "parts[4].id",
		"ClothesPants", "parts[5].id",
		"ClothesSocks", "parts[6].id",
		"ClothesShoesInner", "parts[7].id",
		"ClothesShoesOuter", "parts[8].id",

		"ClothesJacketSubA", "subPartsId[0]",
		"ClothesJacketSubB", "subPartsId[1]",
		"ClothesJacketSubC", "subPartsId[2]",

		"ClothesSailorSubA", "subPartsId[0]",
		"ClothesSailorSubB", "subPartsId[1]",
		"ClothesSailorSubC", "subPartsId[2]",

	};
	public string translate(string from)
	{
		var orig = from;
		for (int i = 0; i < tab.Length; i += 2)
		{
			if (from.EndsWith(tab[i]))
			{
				from = from.Replace(tab[i], tab[i + 1]);
				break;
			}
		}
		if (from.StartsWith("ChaFileFace."))
			return from.Replace("ChaFileFace.", "custom.face.");
		if (from.StartsWith("ChaFileBody."))
			return from.Replace("ChaFileBody.", "custom.body.");
		if (from.StartsWith("ChaFileHair"))
			return from.Replace("ChaFileHair", "custom.hair");

		var parts = from.Split('.');
		if (parts[0].StartsWith("outfit"))
		{
			var pfx = $"coodinate[{parts[0].Substring(6)}]";
			if (parts[1].StartsWith("accessory"))
				return $"{pfx}.accessory.parts[{parts[1].Substring(9)}].id";
			if (parts[1] == "ChaFileMakeup")
				return $"{pfx}.makeup.{parts[2]}";
			if (parts[1] == "ChaFileClothes")
				return $"{pfx}.clothes.{parts[2]}.id";
		}
		print($"Ooops, don't know how to translate {orig}");
		return orig;
	}


	[MessagePackObject(true)]
	public class ResolveInfo
	{
		public string ModID;
		public int Slot;
		public string Property;
		public int Category;
	}

	[Prio(1000)]
	public override void OnCardLoad(ChaFile f, BlockHeader bh, bool nopng, bool nostatus)
	{
		try
		{
			TryImport(f.dict.Get<KKEx>("kkex"), f.dict.Get<FakeID.GuidMap>("guidmap"));
		}
		catch { };
	}

	public void TryImport(KKEx k, FakeID.GuidMap map) {
		var guids = k.data["com.bepis.sideloader.universalautoresolver"].data["info"] as object[];
		foreach (var entry in guids)
		{
			var e = MessagePackSerializer.Deserialize<ResolveInfo>(entry as byte[]);
			var prop = translate(e.Property);
			if (prop == e.Property)
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

}
