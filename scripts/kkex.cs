//@INFO: Bepinex card data support
//@VER: 1

using MessagePack;
using System;
using System.Collections.Generic;
using System.IO;
using ExtensibleSaveFormat; //bikeshed.cs
using Patchwork;

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

public class ImportKKEx : ScriptEvents
{
	public override void Awake()
	{
		ExtendedSave.GetAllExtendedDataCB = (file) => file.dict.Get<KKEx>("kkex").data;
	}

	[Prio(2000)]
	public override void OnCardLoad(ChaFile f, BlockHeader bh, bool nopng, bool nostatus)
	{
		var k = new KKEx();
		if (bh.Load(k))
			f.dict.dict["kkex"] = k;
		try
		{
			Ext.Raise<ExtendedSave>(null, "CardBeingLoaded", f);
		}
		catch { };
	}

	public override void OnCardSave(ChaFile f, BinaryWriter w, List<object> blocks, bool nopng)
	{
		blocks.Add(f.dict.Get<KKEx>("kkex"));
		try
		{
			Ext.Raise<ExtendedSave>(null, "CardBeingSaved", f);
		}
		catch { };
	}
}

