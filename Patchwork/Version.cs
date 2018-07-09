using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

public static partial class Patchwork
{
	public static int version
	{
		get
		{
			return Assembly.GetExecutingAssembly().GetName().Version.Major;
		}
	}

	public static string mkver
	{
		get
		{
			var ver = Assembly.GetExecutingAssembly().GetName().Version;
			return " Mk." + ver.Major + (ver.Minor != 0 ? $"({ver.Minor})" : "");
		}
	}

	public static bool isStudio
	{
		get
		{
			return UnityEngine.Application.productName == "CharaStudio";
		}
	}
	public static string prefix
	{
		get
		{
			return isStudio ? "Studio" : "";
		}
	}
}
