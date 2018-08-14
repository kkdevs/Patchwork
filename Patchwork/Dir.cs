public static class Dir
{
	public static string root;
	public static string exe => root + (Patchwork.isStudio ? "CharaStudio.exe" : "Koikatu.exe");
	public static string data => root + (Patchwork.isStudio ? "CharaStudio_Data" : "Koikatu_Data");
	public static string pw;
	public static string mod;
	public static string modcsv;
	public static string cache;
	public static string mat;
	public static string hist;
	public static string conf;
	public static string abdata;
	public static string map;
	public static string userdata;
	public static void Init(string path)
	{
		path = path.Replace("\\", "/");
		Debug.Log(path);
		root = path;
		userdata = path + "userdata/";
		pw = root + "patchwork/";
		mod = pw + "mod/";
		modcsv = mod + "!csv/";
		cache = pw + "cache/";
		mat = pw + "material/";
		hist = pw + "history.xml";
		conf = pw + "config.xml";
		map = pw + "map/";
		abdata = root + "abdata/";
	}
}

