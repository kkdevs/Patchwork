using Patchwork;
using Newtonsoft.Json;

public static partial class Extensions {
	public static string ToJSON(this object o) {
		var settings = new JsonSerializerSettings()
		{
			ReferenceLoopHandling = ReferenceLoopHandling.Ignore
		};
		return JsonConvert.SerializeObject(o, Formatting.Indented, settings);
	}
}

public partial class ScriptEnv : Script
{
	public static string PrettyPrint(object o)
	{
		return o.ToJSON();
	}
	public static new void pp(object o)
	{
		print(PrettyPrint(o));
	}
}
