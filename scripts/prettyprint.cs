using Patchwork;
using Newtonsoft.Json;

public static partial class Extensions {
	public static string ToJSON(this object o) {
		return JsonConvert.SerializeObject(o, Formatting.Indented);
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
