//@INFO: Dump material properties and textures
//@VER: 1
//@OFF

using static Patchwork;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

public class MatDump : ScriptEvents
{
	public static Dictionary<string, GameObject> fbx = new Dictionary<string, GameObject>();
	public static List<string> texNames  = new List<string>()
		{
			"MainTex",
			"LineMask",
			"ColorMask",
			"NormalMap",
			"AnotherRamp",
			"GlassRamp",
			"MetallicGlossMap",
			"ParallaxMap",
			"OcclusionMap",
			"EmissionMap",
			"DetailMask",
			"DetailAlbedoMap",
			"DetailNormalMap",
			"BumpMap",
			"EmissionMap",
			//TBD
		};
	public static List<string> floatNames = new List<string>()
	{
			"Cutoff",
			"Glossiness",
			"BumpScale",
			"Metallic",
			"Parallax",
			"OcclusionStrength",
			"rimV",
			"SrcBlend",
			"Mode",
			// TBD
	};
	public static List<string> colorNames = new List<string>()
	{
			"Color",
			"Color1",
			"Color2",
			"Color3",
			"Color4",
			"Emission",
			"Shadow",
			// TBD
	};

	// not really needed yet
	public static List<string> vectorNames = new List<string>()
	{
	};

	public static List<string> matrixNames = new List<string>()
	{
	};


	public static string dest;
	public static Material mat;
	public static StringBuilder sb = new StringBuilder();
	public void SaveCSV(string name, string columns, IEnumerable<string> names, System.Action<string> fmt)
	{
		sb.Length = 0;
		foreach (var prop in names)
		{
			if (!mat.HasProperty("_" + prop))
				continue;
			sb.Append(prop + ",");
			fmt(prop);
		}
		if (sb.Length > 0)
			File.WriteAllBytes(dest + name + ".csv", (columns + "\n" + sb.ToString()).ToBytes());
	}
	public override void OnLoadFBX(ChaControl ctrl, ref GameObject go, string ab = null, string ass = null, ListInfoBase lib = null)
	{
		if (go == null) return;
		fbx[go.name] = go;
		sb.Length = 0;
		
		foreach (var r in go.GetComponentsInChildren<Renderer>())
		{
			mat = r.material;
			var mname = mat.name.Replace(" (Instance)", "");
			dest = Dir.mat + mname + "/";
			Directory.CreateDirectory(dest);
			foreach (var tname in texNames)
			{
				var pngname = dest + tname + ".png";
				if (File.Exists(pngname)) // avoid slowness
					continue;
				var n = "_" + tname;
				var tex = mat.GetTexture(n);
				if (tex == null)
					continue;
				var save = RenderTexture.active;
				var tmp = RenderTexture.GetTemporary(tex.width, tex.height);
				Graphics.Blit(tex, tmp);
				RenderTexture.active = tmp;
				Texture2D ntex = new Texture2D(tex.width, tex.height);
				ntex.ReadPixels(new Rect(0, 0, tex.width, tex.height), 0, 0);
				RenderTexture.active = save;
				File.WriteAllBytes(pngname, ntex.EncodeToPNG());
			}

			
			SaveCSV("float", "Name,Value", floatNames, (x) => sb.AppendLine(mat.GetFloat(x).ToString()));
			SaveCSV("vector", "Name,X,Y,Z,W", vectorNames, (x) =>
			{
				var v = mat.GetVector(x);
				sb.AppendLine($"{v.x},{v.y},{v.z},{v.w}");
			});
			SaveCSV("color", "Name,R,G,B,A", colorNames, (x) =>
			{
				var v = mat.GetColor(x);
				sb.AppendLine($"{v.r},{v.g},{v.b},{v.a}");
			});
			SaveCSV("matrix", "Name,X,Y,Z,T", matrixNames, (x) =>
			{
				var m = mat.GetMatrix(x);
				for (int i = 0; i < 4; i++)
				{
					if (i > 0)
						sb.Append(",");
					sb.AppendLine($"{m[i, 0]},{m[i, 1]},{m[i, 2]},{m[i, 3]}");
				}
			});
			if (Directory.GetFiles(dest).Length == 0)
				Directory.Delete(dest);
		}
	}
}