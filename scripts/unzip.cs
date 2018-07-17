//@INFO: Compatibility for zipmods
//@VER: 2

using static Patchwork;
using System.IO;
using ICSharpCode.SharpZipLib.Zip;
using System.Xml;
using System.Text;
using UnityEngine;
using System.Linq;
using System.Security.Cryptography;
using System.Collections.Generic;

public class unzip : ScriptEvents
{
	static byte []entry2bytes(ZipFile zf, ZipEntry entry)
	{
		return new BinaryReader(zf.GetInputStream(entry)).ReadBytes((int)entry.Size);
	}
	[Prio(99999)]
	public override void Awake()
	{
		if (!settings.loadMods) return;
		var target = Dir.mod;
		print("Unzipping mods into " + Path.GetFullPath(target));
		var zipdir = Dir.root + "mods";
		bool needRescan = false;
		LoadedAssetBundle.GCBundles();
		foreach (var zipfn in Directory.GetFiles(zipdir, "*.zip", SearchOption.AllDirectories))
		{
			var modname = Path.GetFileNameWithoutExtension(zipfn);
			//print("@ " + modname);
			if (Directory.Exists(target + modname))
				continue;
			needRescan = true;
			print("Extracting " + Path.GetFileName(zipfn));
			using (var fs = File.Open(zipfn, FileMode.Open, FileAccess.Read, FileShare.Read))
			{
				var zip = new ZipFile(fs);
				var guid = modname;

				// pick guid from manifest if its there
				var manifest = zip.GetEntry("manifest.xml");
				if (manifest != null)
				{
					XmlDocument doc = new XmlDocument();
					doc.LoadXml(Encoding.UTF8.GetString(entry2bytes(zip, manifest)).StripBOM());
					guid = doc.SelectSingleNode("//guid").InnerText;
				}
				foreach (ZipEntry entry in zip)
				{
					if (!entry.IsFile) continue;
					var efn = entry.Name;
					if (efn.ToLower().EndsWith("manifest.xml"))
						continue;
					
					if (efn.StartsWith("abdata/"))
						efn = efn.Substring(7);
					
					var prefix = "list/characustom/";
					var basename = Path.GetFileNameWithoutExtension(efn);
					var bytes = entry2bytes(zip, entry);

					// make the cat manifest csv canonical
					if (efn.StartsWith(prefix) && efn.EndsWith(".csv"))
					{
						var str = Encoding.UTF8.GetString(bytes);
						var ex = ScriptableObject.CreateInstance<ExcelData>().Import(CSV.ParseCSV(str));
						var firstrow = ex.list.First().list;
						var cat = -1;
						var dist = 0;
						if (firstrow.Count == 1)
						{
							try
							{
								cat = int.Parse(ex[0, 0].Trim());
								dist = int.Parse(ex[1, 0].Trim());
								ex.list.RemoveRange(0, 3);
							}
							catch (System.Exception exc) {
								print(exc.Message);
								print(ex[0, 0]);
								print(ex[1, 0]);
							};
						}
						else
						{
							try
							{
								cat = int.Parse(basename.Split('_')[0]);
							}
							catch { };
						}
						// if we can't figure out the category, bail on this csv
						if (cat == -1)
						{
							print(zipfn + " unzip failed");
							continue;
						}
						efn = $"{prefix}{dist:D2}_{guid}/{cat}_{basename}.csv";
						bytes = ex.Marshal().AddBOM().ToBytes();
					}

					// if it is a hardmod, check that the contents of the bundle fully match the file overriden.
					var hardab = Dir.abdata + efn;
					var oldefn = efn;
					int nmissing = 0;
					efn = target + modname + "/" + efn;
					Directory.CreateDirectory(Path.GetDirectoryName(efn));
					bool nukecab = true;
					if (efn.EndsWith(".unity3d") && File.Exists(hardab))
					{
						// if it overwrites in its entirety, do not nuke the cab as deps will point to it
						nukecab = false;
						var ab = AssetBundle.LoadFromMemory(bytes);
						if (ab == null)
						{
							print($"WARNING: {efn} failed to load, skipping.");
							continue;
						}
						var abnames = new HashSet<string>(ab.GetAllAssetNames().Select((x) => Path.GetFileNameWithoutExtension(x)).ToList());
						ab.Unload(true);

						var origab = AssetBundle.LoadFromFile(hardab);
						if (origab == null)
							goto skip2;

						// Now load the original and check the replacement has all its assets
						foreach (var ass in origab.GetAllAssetNames())
						{
							var shass = Path.GetFileNameWithoutExtension(ass);
							if (!abnames.Contains(shass))
							{
								//print($"WARNING: {Path.GetFileName(efn)}: {shass} is missing {oldefn}");
								nmissing++;
							}
						}
						origab.Unload(true);

						// missing assets; nuke cab
						if (nmissing > 0)
							nukecab = true;
					}
					skip2:

					//print(".. Extracted " + efn);
					if (nmissing != 0)
						efn = Directory.GetParent(efn).FullName + "/+" + Path.GetFileName(efn);
					if (efn.EndsWith(".unity3d"))
						using (var fo = File.Create(efn))
							if (Vfs.Repack(new MemoryStream(bytes), fo, nukecab))
								bytes = null;
					if (bytes != null)
						File.WriteAllBytes(efn, bytes);
				}
			}
		}
		if (needRescan)
		{
			print("VFS changed; rescanning");
			Vfs.Rescan(false);
			Vfs.Save();
		}
		print("Done");
	}
}
