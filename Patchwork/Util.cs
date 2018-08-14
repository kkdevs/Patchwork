using MessagePack;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using UnityEngine;
using MessagePack.LZ4;
using static Patchwork;
using System.Windows.Forms;
using System.Collections;

public static class Ext
{
	public static List<Control> Find(this Control.ControlCollection ctrl, Func<Control, string, bool> pred, bool recurse = true, List<Control> res = null)
	{
		res = res ?? new List<Control>();
		foreach (Control c in ctrl)
		{
			string name = (c.Tag is System.String) ? c.Tag as string : c.Name;
			if (pred(c, name))
				res.Add(c);
			if (recurse && c.HasChildren)
				Find(c.Controls, pred, recurse, res);
		}
		return res;
	}


	public static void RunCoroutine(IEnumerator en)
	{
		while (en.MoveNext())
			if (en.Current is IEnumerator enc)
				RunCoroutine(enc);
	}
	public static IEnumerable<Control> GetAll(this Control control, Type type)
	{
		var controls = control.Controls.Cast<Control>();

		return controls.SelectMany(ctrl => ctrl.GetAll(type))
								  .Concat(controls)
								  .Where(c => c.GetType() == type);
	}
	public static byte[] LZ4Compress(byte[] buf)
	{
		//return buf;
		var outbuf = new byte[LZ4Codec.MaximumOutputLength(buf.Length)];
		int len = LZ4Codec.Encode(buf, 0, buf.Length, outbuf, 0, outbuf.Length);
		Array.Resize(ref outbuf, len);
		return outbuf;
	}

	public static byte[] LZ4Decompress(byte[] buf, byte[] outbuf)
	{
		//return buf;
		LZ4Codec.Decode(buf, 0, buf.Length, outbuf, 0, outbuf.Length);
		return outbuf;
	}

	public static byte[] LZ4Decompress(byte[] input, int inoff, int inlen, int outlen)
	{
		var outbuf = new byte[outlen];
		LZ4Codec.Decode(input, inoff, inlen, outbuf, 0, outlen);
		return outbuf;
	}

	public static byte[] LZ4Compress(byte[] input, int inoff, int inlen)
	{
		var outbuf = new byte[LZ4Codec.MaximumOutputLength(inlen)];
		int olen = LZ4Codec.Encode(input, inoff, inlen, outbuf, 0, outbuf.Length);
		Array.Resize(ref outbuf, olen);
		return outbuf;
	}
	public static uint bswap(uint x)
	{
		x = (x >> 16) | (x << 16);
		return ((x & 0xFF00FF00) >> 8) | ((x & 0x00FF00FF) << 8);
	}
	public static ulong bswap(ulong x)
	{
		x = (x >> 32) | (x << 32);
		x = ((x & 0xFFFF0000FFFF0000) >> 16) | ((x & 0x0000FFFF0000FFFF) << 16);
		return ((x & 0xFF00FF00FF00FF00) >> 8) | ((x & 0x00FF00FF00FF00FF) << 8);
	}
	public static void Put(this BinaryWriter w, string s)
	{
		w.Write(Encoding.UTF8.GetBytes(s));
		w.Write((byte)0);
	}
	public static string GetString(this BinaryReader r)
	{
		var sb = new StringBuilder();
		char c;
		while ((c = r.ReadChar()) != 0)
			sb.Append(c);
		return sb.ToString();
	}
	public static void Put(this BinaryWriter w, int v) => w.Write(bswap((uint)v));
	public static void Put(this BinaryWriter w, long v) => w.Write(bswap((ulong)v));
	public static void Put(this BinaryWriter w, short v) => w.Write((short)(bswap((uint)v) >> 16));
	public static int GetInt(this BinaryReader r) => (int)bswap(r.ReadUInt32());
	public static short GetShort(this BinaryReader r) => (short)(bswap(r.ReadUInt16()) >> 16);
	public static long GetLong(this BinaryReader r) => (long)bswap(r.ReadUInt64());
	public static MethodInfo GetMethod<T>(string name)
	{
		foreach (var n in typeof(T).GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public))
			if (n.Name == name)
				return n;
		return null;
	}
	public static Dictionary<string, Dictionary<Type, MethodInfo>> methodCache = new Dictionary<string, Dictionary<Type, MethodInfo>>();
	public static MethodInfo CachedGetMethod(this Type t, string name)
	{
		if (!methodCache.TryGetValue(name, out Dictionary<Type, MethodInfo> mdict))
			mdict = methodCache[name] = new Dictionary<Type, MethodInfo>();
		if (mdict.TryGetValue(t, out MethodInfo mi))
			return mi;
		mi = t.GetMethod(name);
		return mdict[t] = mi;
	}

	public static Dictionary<MethodInfo, Dictionary<object, object>> predCache = new Dictionary<MethodInfo, Dictionary<object, object>>();
	public static object Memoize<T>(this T t, Func<T,object> predicate)
	{
		if (!predCache.TryGetValue(predicate.Method, out Dictionary<object, object> dict))
			dict = predCache[predicate.Method] = new Dictionary<object, object>();
		if (!dict.TryGetValue(t, out object tinfo))
			dict[t] = tinfo = predicate(t);
		return tinfo;
	}

	public static Dictionary<Type, MemberInfo[]> varCache = new Dictionary<Type, MemberInfo[]>();
	public static IEnumerable<MemberInfo> GetVars(this Type t)
	{
		MemberInfo[] res;		
		if (!varCache.TryGetValue(t, out res))
		{
			List<MemberInfo> tmp = new List<MemberInfo>();
			foreach (var v in t.GetFields())
				tmp.Add(v);
			foreach (var gs in t.GetProperties())
				tmp.Add(gs);
			varCache[t] = res = tmp.ToArray();
		}
		return res;
	}
	public static Dictionary<MemberInfo, Type> memTypeCache = new Dictionary<MemberInfo, Type>();
	public static Dictionary<MemberInfo, string> memNameCache = new Dictionary<MemberInfo, string>();
	public static string GetName(this MemberInfo m)
	{
		if (memNameCache.TryGetValue(m, out string name))
			return name;
		return memNameCache[m] = m.Name;
	}
	public static Type GetVarType(this MemberInfo m)
	{
		if (memTypeCache.TryGetValue(m, out Type res))
			return res;
		var f = m as FieldInfo;
		if (f != null)
			return memTypeCache[m] = f.FieldType;
		var p = m as PropertyInfo;
		if (p != null)
			return memTypeCache[m] = p.PropertyType;
		return memTypeCache[m] = null;
		//throw new Exception("Invalid member type");
	}
	public static object GetValue(this MemberInfo m, object o)
	{
		var f = m as FieldInfo;
		if (f != null)
			return f.GetValue(o);
		var p = m as PropertyInfo;
		if (p != null && p.CanRead)
			return p.GetValue(o, null);
		throw new Exception("Invalid member type for " + m.Name);
	}
	public static bool IsString(this Type t)
	{
		return typeof(String) == t;//.IsAssignableFrom(t);
	}
	public static bool IsBasic(this Type t)
	{
		return t.IsPrimitive || t.IsString();
	}

	public static Dictionary<Type, Dictionary<MemberInfo, object>> attrCache = new Dictionary<Type, Dictionary<MemberInfo, object>>();
	public static bool GetAttr<T>(this MemberInfo m, ref T ret) where T : class
	{
		if (!attrCache.TryGetValue(typeof(T), out Dictionary<MemberInfo, object> mdict))
			mdict = attrCache[typeof(T)] = new Dictionary<MemberInfo, object>();
		if (mdict.TryGetValue(m, out object retval))
			return (ret = (retval ?? ret) as T) != null;
		return (ret = (mdict[m] = (m.GetCustomAttributes(typeof(T), true).FirstOrDefault() as T) ?? ret) as T) != null;
	}
	public static string GetSignature(this MethodInfo method, bool callable = false)
	{
		var firstParam = true;
		var sigBuilder = new StringBuilder();
		if (callable == false)
		{
			if (method.IsPublic)
				sigBuilder.Append("public ");
			else if (method.IsPrivate)
				sigBuilder.Append("private ");
			else if (method.IsAssembly)
				sigBuilder.Append("internal ");
			if (method.IsFamily)
				sigBuilder.Append("protected ");
			if (method.IsStatic)
				sigBuilder.Append("static ");
			sigBuilder.Append(TypeName(method.ReturnType));
			sigBuilder.Append(' ');
		}
		sigBuilder.Append(method.Name);

		// Add method generics
		if (method.IsGenericMethod)
		{
			sigBuilder.Append("<");
			foreach (var g in method.GetGenericArguments())
			{
				if (firstParam)
					firstParam = false;
				else
					sigBuilder.Append(", ");
				sigBuilder.Append(TypeName(g));
			}
			sigBuilder.Append(">");
		}
		sigBuilder.Append("(");
		firstParam = true;
		var secondParam = false;
		foreach (var param in method.GetParameters())
		{
			if (firstParam)
			{
				firstParam = false;
				if (method.IsDefined(typeof(System.Runtime.CompilerServices.ExtensionAttribute), false))
				{
					if (callable)
					{
						secondParam = true;
						continue;
					}
					sigBuilder.Append("this ");
				}
			}
			else if (secondParam == true)
				secondParam = false;
			else
				sigBuilder.Append(", ");
			if (param.ParameterType.IsByRef)
				sigBuilder.Append("ref ");
			else if (param.IsOut)
				sigBuilder.Append("out ");
			if (!callable)
			{
				sigBuilder.Append(TypeName(param.ParameterType));
				sigBuilder.Append(' ');
			}
			sigBuilder.Append(param.Name);
		}
		sigBuilder.Append(")");
		return sigBuilder.ToString();
	}

	/// <summary>
	/// Get full type name with full namespace names
	/// </summary>
	/// <param name="type">Type. May be generic or nullable</param>
	/// <returns>Full type name, fully qualified namespaces</returns>
	public static string TypeName(Type type)
	{
		var nullableType = Nullable.GetUnderlyingType(type);
		if (nullableType != null)
			return nullableType.Name + "?";

		if (!(type.IsGenericType && type.Name.Contains('`')))
			switch (type.Name)
			{
				case "String": return "string";
				case "Int32": return "int";
				case "Decimal": return "decimal";
				case "Object": return "object";
				case "Void": return "void";
				default:
					{
						return type.FullName.IsNullOrWhiteSpace() ? type.Name : type.FullName;
					}
			}

		var sb = new StringBuilder(type.Name.Substring(0,
		type.Name.IndexOf('`'))
		);
		sb.Append('<');
		var first = true;
		foreach (var t in type.GetGenericArguments())
		{
			if (!first)
				sb.Append(',');
			sb.Append(TypeName(t));
			first = false;
		}
		sb.Append('>');
		return sb.ToString();
	}
	public static object Raise<T>(object inst, string name, params object[] args)
	{
		return (typeof(T).GetField(name, BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).GetValue(inst) as Delegate)?.DynamicInvoke(args);
	}

	public static Dictionary<Assembly, string> locSpoof;

	public class AssHook : EzHook<AssHook>
	{
		public static MethodInfo get_location = GetMethod<Assembly>("get_location");
		public static string get_Location(Assembly ass)
		{
			if (locSpoof.TryGetValue(ass, out string path))
				return path;
			return get_location.Invoke(ass, new object[] { }) as string;
		}
	}

	public static Assembly LoadAssembly(string fn)
	{
		var ass = Assembly.Load(File.ReadAllBytes(fn));
		if (ass != null)
		{
			if (locSpoof == null)
			{
				locSpoof = new Dictionary<Assembly, string>();
				AssHook.ApplyTo(ass.GetType());
			}
			locSpoof[ass] = fn;
		}
		else ass = Assembly.LoadFile(fn);
		return ass;
	}
	public static string LoadTextFile(string f)
	{
		try
		{
			return Encoding.UTF8.GetString(File.ReadAllBytes(f)).StripBOM();
		}
		catch
		{
			return null;
		}
	}

	public static bool HasAttr<T>(this MemberInfo m) where T : class
	{
		T dummy = null;
		return GetAttr(m, ref dummy);
	}
	public static void SetValue(this MemberInfo m, object o, object v)
	{
		var f = m as FieldInfo;
		if (f != null)
		{
			f.SetValue(o, v);
			return;
		}
		var p = m as PropertyInfo;
		if (p != null && p.CanWrite)
		{
			p.SetValue(o, v, null);
			return;
		}
		throw new Exception("Invalid member type for " + m.Name);
	}

	public static bool Cmp(byte[] a, byte[] b) {
		if (a.Length != b.Length)
			return false;

		for (int j = 0; j<a.Length; j++)
			if (a[j] != b[j])
				return false;
		return true;
	}

	public static bool Cmp<T>(T a, T b) where T : class
	{
		return Cmp(MessagePackSerializer.Serialize(a), MessagePackSerializer.Serialize(b));
	}

	public static Type[] GetTypesSafe(this Assembly ass)
	{
		try
		{
			return ass.GetTypes();
		} catch (Exception ex) {
			Debug.Log(ex);
			try
			{
				return ass.GetExportedTypes();
			} catch (Exception ex2)
			{
				Debug.Log(ex2);
				return new Type[] { };
			}
		}
	}
	public static Vector3 ReadVector3(this BinaryReader br)
	{
		return new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
	}
	public static void Write(this BinaryWriter bw, Vector3 vec)
	{
		bw.Write(vec.x);
		bw.Write(vec.y);
		bw.Write(vec.z);
	}
	public static IEnumerable<string> GetFilesMulti(IEnumerable<string> path, string mask)
	{
		foreach (var pa in path)
			if (Directory.Exists(pa))
				foreach (var f in Directory.GetFiles(pa, mask))
					yield return f;
	}


	public static void LWrite(this BinaryWriter bw, byte[] buf)
	{
		buf = buf ?? new byte[0];
		bw.Write(buf.Length);
		bw.Write(buf);
	}

	public static MethodInfo serialize;
	public static MethodInfo lz4serialize;
	public static void Init()
	{
		/*serialize = InitMsgPack(typeof(MessagePackSerializer));
		lz4serialize = InitMsgPack(typeof(LZ4MessagePackSerializer));
		Debug.Log(serialize);
		Debug.Log(lz4serialize);*/
		//EzHook.Redirect(typeof(Ext), "get_Location", typeof(Assembly), "LocationSpoofer");
	}

	public static MethodInfo InitMsgPack(Type ct)
	{
		return ct.GetMethods().First(t => t.Name == "Serialize" && t.GetParameters().Count() == 2);
	}


	public static byte[] SerializeObject(object o, bool lz4)
	{
		if (lz4)
			return LZ4MessagePackSerializer.NonGeneric.Serialize(o.GetType(), o, MessagePack.Resolvers.ContractlessStandardResolver.Instance);
		else
			return MessagePackSerializer.NonGeneric.Serialize(o.GetType(), o, MessagePack.Resolvers.ContractlessStandardResolver.Instance);
		//var ser = lz4 ?	 : serialize;
		//return ser.MakeGenericMethod(o.GetType()).Invoke(null, new object[] { o, MessagePack.Resolvers.ContractlessStandardResolver.Instance}) as byte[];
	}

	public static byte[] Compress(byte[] input)
	{
		var buf = new MemoryStream();
		var gzip = new DeflateStream(buf, CompressionMode.Compress);
		gzip.Write(input, 0, input.Length);
		gzip.Close();
		return buf.ToArray();
	}
	public static byte[] Decompress(byte[] buf)
	{
		var gzip = new DeflateStream(new MemoryStream(buf), CompressionMode.Decompress);
		return gzip.CopyTo(new MemoryStream()).ToArray();
	}


	public static Expression[] CreateParameterExpressions(this MethodInfo method, Expression argumentsParameter)
	{
		return method.GetParameters().Select((parameter, index) =>
			Expression.Convert(
				Expression.ArrayIndex(argumentsParameter, Expression.Constant(index)),
				parameter.ParameterType)).ToArray();
	}

	public static T CopyTo<T>(this Stream source, T destination, int bufferSize = 81920) where T : Stream
	{
		byte[] array = new byte[bufferSize];
		int count;
		while ((count = source.Read(array, 0, array.Length)) != 0)
			destination.Write(array, 0, count);
		return destination;
	}



	public static byte[] LRead(this BinaryReader br)
	{
		int n = br.ReadInt32();
		return br.ReadBytes(n);
	}
	public static string StripBOM(this string str)
	{
		if (str[0] == '\uFEFF')
			return str.Substring(1);
		return str;
	}
	public static byte []ToBytes(this string str)
	{
		return Encoding.UTF8.GetBytes(str);
	}
	public static IEnumerable<T> PlusOne<T>(this IEnumerable<T> a, T plus)
	{
		foreach (var v in a)
			yield return v;
		if (plus != null)
			yield return plus;
	}
	public static string HashToString(byte[] allBytes)
	{
		var md5 = System.Security.Cryptography.MD5.Create();
		return Convert.ToBase64String(md5.ComputeHash(allBytes)).Replace("/", "").Replace(".", "").Replace("+", "");
	}
	public static string AddBOM(this string str)
	{
		if (settings.useBOM)
		{
			if (str[0] == '\uFEFF')
				return str;
			return '\uFEFF' + str;
		}
		else return str.StripBOM();
	}
	public static bool IsList(this Type ft)
	{
		if (!ft.IsGenericType)
			return false;
		if (ft.GetGenericTypeDefinition() != typeof(List<>))
			return false;
		return true;
	}
	/*public static bool IsNullOrWhiteSpace(this String s)
	{
		if (s == null)
			return true;
		if (s.Trim() == "")
			return true;
		return false;
	}*/
}




