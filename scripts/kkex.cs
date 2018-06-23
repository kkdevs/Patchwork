//@INFO: Bepinex card data support
//@VER: 1

using MessagePack;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using ExtensibleSaveFormat;
using static ChaListDefine;

public class KKEx
{
	public static string Version => "3";
	public static string BlockName = "KKEx";
	public Dictionary<string, PluginData> data = new Dictionary<string, PluginData>();
	public byte[] SaveBytes() => MessagePackSerializer.Serialize(data);
	public void LoadBytes(byte[] buf, Version ver)
	{
		data = MessagePackSerializer.Deserialize<Dictionary<string, PluginData>>(buf);
	}
}

public class ImportKKEx : MonoBehaviour
{
	public void OnCardLoad(ChaFile f, BlockHeader bh, bool nopng, bool nostatus)
	{
		var k = new KKEx();
		if (bh.Load(k))
			f.dict.dict["kkex"] = k;
	}

	public void OnCardSave(ChaFile f, BinaryWriter w, List<object> blocks, bool nopng)
	{
		blocks.Add(f.dict.Get<KKEx>("kkex"));
	}
}

//bikeshed
namespace ExtensibleSaveFormat
{
	[MessagePackObject]
	public class PluginData
	{
		[Key(0)]
		public int version;
		[Key(1)]
		public Dictionary<string, object> data = new Dictionary<string, object>();
	}
	public class ExtendedSave
	{
		public static Dictionary<string, PluginData> GetAllExtendedData(ChaFile file) => file.dict.Get<KKEx>("kkex").data;
		public static PluginData GetExtendedDataById(ChaFile file, string id)
		{
			PluginData res;
			return GetAllExtendedData(file).TryGetValue(id, out res) ? res : null;
		}
		public static void SetExtendedDataById(ChaFile file, string id, PluginData d)
		{
			GetAllExtendedData(file)[id] = d;
		}
	}
}