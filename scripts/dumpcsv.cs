﻿using ParadoxNotion.Serialization.FullSerializer;
using Patchwork;
using System;
using System.IO;
using System.Linq;
using UnityEngine;

public partial class ScriptEnv
{
	public static void dumpallcsv()
	{
		string[] csvdirs =
		new string[] {
			"action/actioncontrol",
			"adv",
			"communication",
			"custom",
			"h/list",
			"scene",
			"list",
			"map/list",
		};
		var mainass = AppDomain.CurrentDomain.GetAssemblies().First(a => a.GetName().Name == "Assembly-CSharp");
		var dumpables = mainass.GetExportedTypes().Where(t => typeof(IDumpable).IsAssignableFrom(t));
		var basedir = LoadedAssetBundle.basePath;
		print($"Found {dumpables.Count()} serializable types.");
		foreach (var dir in csvdirs) {
			foreach (var f in Directory.GetFiles(basedir + dir, "*.unity3d", SearchOption.AllDirectories))
			{
				var abname = f.Substring(basedir.Length);
				if (abname.Contains("--"))
					continue;
				LoadedAssetBundle ab = null;
				try
				{
					ab = LoadedAssetBundle.Load(abname);
				}
				catch { };
				if (ab == null) continue;
				print($"Dumping {abname}");
				foreach (var aname in ab.GetAllAssetNames())
				{
					var shorted = Path.GetFileNameWithoutExtension(aname);
					// try to dump lsts speculatively
					var res = GlobalMethod.LoadAllListText(abname, shorted);
					if (res == "@garray")
						continue;
					// or one of the serializable types
					foreach (var typ in dumpables) {
						AssetBundleLoadAssetOperation dummy;
						try
						{
							var ok = Cache.Asset(abname, shorted, typ, null, out dummy);
							if (ok && !dummy.IsEmpty())
								break;
						} catch { };
					}
				}
				ab.Unload(true);
			}
		}
	}
}