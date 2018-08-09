//@INFO: Compatibility for mods
//@VER: 3

// Patchwork completely replaces game subsystems for loading data, mainly for performance
// (for ABs) and extensibility (csvs) reasons.
//
// The loader (Vfs.cs) still uses canonical format both for all asset bundles as well as csv metadata,
// retaining the original structure as virtual filesystem tree and via reflection respectively.
//
// The way bepinex inserts mod data is different - it has limited set of special-cased hooks for
// various mod data injected and frequently uses ad-hoc, manually written serializers and bundle handlers
// unrelated to the original structure at hand.
//
// Instead of special-casing every little thing this way in our asset loading code, we can convert from the
// ad-hoc bepinex format back to canonical representation in here so as to keep the actual loader code generic.

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
	public bool sideloader_compat = true;
	static byte []entry2bytes(ZipFile zf, ZipEntry entry)
	{
		return new BinaryReader(zf.GetInputStream(entry)).ReadBytes((int)entry.Size);
	}
	[Prio(99999)]
	public override void Awake()
	{
		if (!settings.loadMods) return;
		var needRescan = false;
		needRescan |= CanonizeZip();
		needRescan |= CanonizeCsv();
		needRescan |= CopyABdata();
		if (needRescan)
		{
			print("VFS changed; rescanning");
			LoadedAssetBundle.GCBundles();
			Vfs.Rescan(true);
			Vfs.Save();
		}
	}

	public bool CopyABdata()
	{
		
		var target = Dir.mod + "abdata/";
		if (settings.withoutManifest && Directory.Exists(target)) return false;
		bool res = false;
		foreach (var sfn in Directory.GetFiles(Dir.abdata, "*.unity3d", SearchOption.AllDirectories))
		{
			var fn = sfn.Replace("\\", "/").Substring(Dir.abdata.Length);
			if (LoadedAssetBundle.cache.TryGetValue(fn, out LoadedAssetBundle ab))
			{
				if (ab.hasRealManifest) continue;
				if (ab.name.StartsWith("sound/")) continue;
			}

			var fout = target + fn;
			if (File.Exists(fout)) continue;
			Directory.CreateDirectory(target + fn.Remove(fn.LastIndexOf('/')));
			var fin = Dir.abdata + fn;
			if (Vfs.Repack(fin, fout, true))
			{
				res = true;
				print($"Moved {fn}");
			}
			else
			{
				print($"Failed to move {fn}");
				File.Delete(fout);
			}
		}	
		return res;
	}

	public bool CanonizeCsv()
	{
		var ret = false;
		var source = Dir.root + "bepinex/translation/scenario/";
		var target = Dir.mod + "translation/adv/scenario/";
		if (Directory.Exists(source) && !Directory.Exists(target))
		{
			ret = true;
			Directory.CreateDirectory(target);
			foreach (var csv in Directory.GetFiles(source, "*.csv", SearchOption.AllDirectories))
			{
				var dir = csv.Remove(csv.Replace("\\","/").LastIndexOf('/'));
				if (!dir.EndsWith("/00"))
					dir += "/00";
				dir = target + dir.Substring(source.Length);
				Directory.CreateDirectory(dir);
				var data = "_hash,_version,_command,_multi,_args\n" + Encoding.UTF8.GetString(File.ReadAllBytes(csv)).StripBOM();
				File.WriteAllBytes(dir + "/" + Path.GetFileName(csv), data.ToBytes());
			}
		}
		source = Dir.root + "bepinex/translation/communication/";
		target = Dir.mod + "translation/communication/info_99/";
		if (Directory.Exists(source) && !Directory.Exists(target))
		{
			ret = true;
			Directory.CreateDirectory(target);
			foreach (var csv in Directory.GetFiles(source, "*.csv", SearchOption.AllDirectories))
				File.Copy(csv, target + Path.GetFileName(csv), true);
		}
		return ret;
	}

	public bool CanonizeZip() {
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
						// if it overwrites in its entirety, do not nuke the cab as deps amy point to it
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
							if (Vfs.Repack(new MemoryStream(bytes), fo, nukecab | sideloader_compat))
								bytes = null;
					if (bytes != null)
						File.WriteAllBytes(efn, bytes);
				}
			}
		}
		print("Done");
		return needRescan;
	}
}
