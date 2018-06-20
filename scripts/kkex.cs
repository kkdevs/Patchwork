//@INFO: Bepinex card data support

using MessagePack;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using static ChaListDefine;

public class KKEx
{
	public static string Version => "3";
	public static string BlockName = "KKEx";
	public Dictionary<string, KeyValuePair<int, Dictionary<string, object>>> data = new Dictionary<string, KeyValuePair<int, Dictionary<string, object>>>();
	public byte[] SaveBytes() => MessagePackSerializer.Serialize(data);
	public void LoadBytes(byte[] buf, Version ver)
	{
		data = MessagePackSerializer.Deserialize<Dictionary<string, KeyValuePair<int, Dictionary<string, object>>>>(buf);
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
