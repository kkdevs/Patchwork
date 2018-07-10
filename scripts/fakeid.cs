//@INFO: Compatibility for Id colliding mods
//@DESC: Rewrite item ids to fake ones so that broken mods still show up
//@VER: 2
//@AFTER: unzip

using System;
using System.IO;
using System.Reflection;
using UnityEngine;
using static Patchwork;
using System.Linq;
using System.Collections.Generic;
using MessagePack;
using static ChaListDefine;


public class FakeID : ScriptEvents
{
	public struct RealPair
	{
		public int cat;
		public int id;
		public RealPair(CategoryNo cat, int id = -1)
		{
			this.cat = (int)cat;
			this.id = id;
		}
		public RealPair(int cat, int id)
		{
			this.cat = cat;
			this.id = id;
		}
	}
	const int FAKE_BASE = -100;
	public override void Start()
	{
		InitListInfo();
	}

	// (re)load the listinfos
	public static IdMap idMap;
	public void InitListInfo()
	{
		knownGuids.Clear();
		idMap = new IdMap();
		if (Manager.Character.Instance != null)
		{
			Manager.Character.Instance.chaListCtrl = new ChaListControl();
			Manager.Character.Instance.chaListCtrl.LoadListInfoAll();
		}
	}

	public IEnumerable<int> GetFakes(int cat, int id)
	{
		if (idMap.real2fake.TryGetValue(new RealPair(cat, id), out List<int> fakes))
			foreach (var f in fakes)
				yield return f;
	}

	public IEnumerable<ListInfoBase> GetInfos(int cat, int fakeid)
	{
		foreach (var f in GetFakes(cat, fakeid))
			yield return idMap.fake2real[f];
		if (idMap.fake2real.TryGetValue(fakeid, out ListInfoBase lib))
			yield return lib;
	}

	public override void OnGetListInfo(ref ListInfoBase lib, int cat, int id)
	{
		lib = lib ?? GetInfos(cat, id).FirstOrDefault();
	}

	public override void OnSetClothes(ChaControl ch, int cat, int[] ids)
	{
		// TODO: maybe other pieces need special handling too
		if (cat != (int)CategoryNo.co_top)
			return;

		var lib = GetInfos(cat, ids[0]).FirstOrDefault();
		if (lib == null) return;
		if (lib.Kind == 1 || lib.Kind == 2)
		{
			int[] def = { 0, 0, 1, 0, 1, 1 };
			int sub = (int)(lib.Kind == 1 ? CategoryNo.cpo_sailor_a : CategoryNo.cpo_jacket_a);
			for (int i = 0; i < 3; i++)
			{
				// Check if supplied value exists
				var inf = GetInfos(sub + i, ids[i+1]).FirstOrDefault();
				if (inf != null && inf.Category == sub + i && ids[i + 1] != 0)
					continue;
				// If not, try to pick something
				int ndef = def[i + (lib.Kind-1)*3];
				ids[i+1] = GetFakes(sub + i, ndef).FirstOrDefault();
			}
		}
	}

	public HashSet<string> knownGuids = new HashSet<string>();
	override public void OnSetListInfo(ListInfoBase lib)
	{
		if ((lib.Category < (int)CategoryNo.bo_head) && (lib.Category != (int)CategoryNo.bodypaint_layout && lib.Category != (int)CategoryNo.facepaint_layout))
			return;
		if (lib.Category == (int)CategoryNo.mt_ramp)
			return;
		if (((lib.Category >= (int)CategoryNo.ao_none) || (lib.Category <= (int)CategoryNo.ao_kokan)) && lib.Id == 0)
			return;
		lib.Id = idMap.NewFake(lib.Category, lib.Id, lib.Clone());
	}

	public class IdMap
	{
		public int counter = FAKE_BASE;
		// map a real <cat,id> pair to list of fake ids
		public Dictionary<RealPair, List<int>> real2fake = new Dictionary<RealPair, List<int>>();
		// map one fake id to real <cat,id,dist> (all contained in infobase)
		public Dictionary<int, ListInfoBase> fake2real = new Dictionary<int, ListInfoBase>();
		public int NewFake(int cat, int realid, ListInfoBase data)
		{
			var realpair = new RealPair(cat, realid);
			List<int> fakeids;
			if (!real2fake.TryGetValue(realpair, out fakeids))
				fakeids = real2fake[realpair] = new List<int>();
			foreach (var item in fakeids)
				if (fake2real[item].Distribution2 == data.Distribution2)
					return item;
			int fakeid = --counter;
			fakeids.Add(fakeid);
			fake2real[fakeid] = data;
			return fakeid;
		}
	}

	public class GuidMap
	{
		public class Item
		{
			public string guid;
			public int cat;
			public int id;
			public string prop;
		}
		public Dictionary<string, Item> items = new Dictionary<string, Item>();

		// translate real to fake, taking into account guid hints.
		// if no hint is present, first fake is used
		public int GetFake(string prop, int cat, int id)
		{
			if (cat == (int)CategoryNo.ao_none || id < 0)
				return id;
			List<int> candidates;
			var realpair = new RealPair(cat, id);
			if (items.TryGetValue(prop, out Item item) && idMap.real2fake.TryGetValue(realpair, out candidates))
			{
				var match = candidates.FirstOrDefault((x) => idMap.fake2real[x].Distribution2 == item.guid);
				if (match != 0)
					return match;
			}
			// nothing found via our guid mappings, so default to a first fake we encounter
			if (idMap.real2fake.TryGetValue(realpair, out candidates))
				return candidates.FirstOrDefault();
			if (cat != 0 && id != 0)
				if (settings.enableSpam)
					print($"Failed to translate real to fake prop={prop} cat={cat} id={id}");
			return id;
		}

		// translate fake id to a real one. at the same time, record guid usage.
		public int GetReal(string prop, int cat, int id)
		{
			ListInfoBase lib;
			if (cat == (int)CategoryNo.ao_none)
				return id;
			if (id >= FAKE_BASE)
				return id;
			if (!idMap.fake2real.TryGetValue(id, out lib) || lib.Category != cat)
			{
				if (cat != 0 && id != 0)
					if (settings.enableSpam)
						print($"Failed to translate fake to real prop={prop} cat({cat}), id={id}");
				return id;
			}
			if (lib.Distribution2.IsNullOrEmpty())
			{ // if no guid now, nuke the mapping
				items.Remove(prop);
			}
			else
			{
				items[prop] = new Item()
				{
					guid = lib.Distribution2,
					cat = cat,
					id = lib.Id,
					prop = prop
				};
			}
			return lib.Id;
		}
	}

	public GuidMap map;
	public bool tofake;
	public int rewrite(string prefix, int cat, int id, string name) => tofake ? map.GetFake(prefix, cat, id) : map.GetReal(prefix, cat, id);


	// traverse object and rewrite ids
	public void traverse(string prefix, object root, int currIdx = 0)
	{
		if (root == null) return;
		var t = root.GetType();

		if (t.IsArray)
		{
			var arr = root as Array;
			int idx = 0;
			if (arr != null && !t.GetElementType().IsBasic())
				foreach (var sub in arr)
					traverse($"{prefix}[{idx}]", sub, idx++);
			return;
		}

		foreach (var mem in (tofake?t.GetVars():t.GetVars().Reverse()))
		{
			var mt = mem.GetVarType();
			if (mt == null) continue;
			var name = mem.GetName();

			if (name == "pattern" || name == "id" || name.EndsWith("Id"))
			{
				CatHint hint = null;
				if (!mem.GetAttr(ref hint))
					continue;
				hint.Reset();
				var cat = hint.Get(root, mem, currIdx);
				var val = mem.GetValue(root);
				var arr = val as Array;
				if (mt == typeof(int))
					mem.SetValue(root, rewrite(prefix + "." + name, cat, (int)val, name));
				else if (arr != null && mt.GetElementType() == typeof(int))
					for (int i = 0; i < arr.Length; i++)
						arr.SetValue(rewrite($"{prefix}.{name}[{i}]", hint.Next(), (int)arr.GetValue(i), name), i);
			}
			else if ((bool)mt.Memoize(IsTraversable))
				traverse(prefix + "." + name, mem.GetValue(root));
		}
	}

	public static object IsTraversable(Type mt)
	{
		if (mt.CachedGetMethod("SaveBytes") != null)
			return true;
		if (mt.IsArray) mt = mt.GetElementType();
		return mt.GetCustomAttributes(typeof(MessagePackObjectAttribute), true).Length > 0;
	}

	public override void OnCardLoad(ChaFile f, BlockHeader bh, bool nopng, bool nostatus)
	{
		tofake = true;
		map = f.dict.Get<GuidMap>("guidmap");
		traverse("coordinate",f.coordinate);
		traverse("custom",f.custom);
	}

	// Note that this is called only when explicitly saving/loading a coordinate.
	public override void OnCoordinate(ChaFile f, ChaFileCoordinate co, bool isLoad)
	{
		tofake = isLoad;
		map = f.dict.Get<GuidMap>("guidmap");
		traverse("coordinate", co);
	}

	// rewrite our fake ids to the actual real ones again
	public override void OnCardSave(ChaFile f, BinaryWriter w, List<object> blocks, bool nopng)
	{
		if (f.dict == null)
			Debug.Log("dict is null");
		map = f.dict.Get<GuidMap>("guidmap");
		if (map == null)
			Debug.Log("map is null");
		map.items.Clear(); // guid mappings will be be-regenerated
		tofake = false;
		foreach (var b in blocks)
		{
			if ((b is Array) && b.GetType().GetElementType() == typeof(ChaFileCoordinate))
				traverse("coordinate", b);
			else if (b is ChaFileCustom)
				traverse("custom", b);
		}
	}
}
