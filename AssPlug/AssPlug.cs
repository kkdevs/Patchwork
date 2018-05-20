using ADV;
using ParadoxNotion.Serialization.FullSerializer;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class AssPlug : MonoBehaviour
{
	void Start()
	{
		foreach (var b in Directory.GetFiles("abdata/communication", "*.unity3d"))
		{
			var bn = b.Substring(7).Replace("\\", "/");
			Debug.Log(bn);
			string res = "";
			foreach (var an in AssetBundleCheck.GetAllAssetName(bn, false))
			{
				var exc = CommonLib.LoadAsset<ExcelData>(bn, an);
				if (exc != null)
				{
					var ban = Path.GetFileNameWithoutExtension(b) + "_" + an;
					File.WriteAllBytes("quoted/" + Path.GetFileNameWithoutExtension(b) + "_" + an + ".csv", System.Text.Encoding.UTF8.GetBytes(exc.GetCSV2()));
				}
			}
		}
	}
}