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

namespace Patchwork
{
	public static class Ext
	{
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
		public static Dictionary<Type, Type> arrayOf = new Dictionary<Type, Type>();
		public static bool IsArrayOf<T>(Type t)
		{
			if (!arrayOf.TryGetValue(t, out Type oft))
				if (t.IsArray)
					oft = arrayOf[t] = t.GetElementType();
			return oft == typeof(T);
		}

		public static Dictionary<Type, MemberInfo[]> varCache = new Dictionary<Type, MemberInfo[]>();
		public static IEnumerable<MemberInfo> GetVars(this Type t, bool reversed)
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
			if (reversed)
				return res.Reverse();
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
			throw new Exception("Invalid member type");
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
			{
				ret = retval as T;
				return ret != null;
			}

			foreach (var attr in m.GetCustomAttributes(true))
			{
				if (attr is T)
				{
					ret = attr as T;
					break;
				}
			}
			mdict[m] = ret;
			return ret != null;
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
			serialize = InitMsgPack(typeof(MessagePackSerializer));
			lz4serialize = InitMsgPack(typeof(LZ4MessagePackSerializer));
			Debug.Log(serialize);
			Debug.Log(lz4serialize);
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

		public static byte[] Compress(byte[] input, CompressionMode mode = CompressionMode.Compress)
		{
			var buf = new MemoryStream();
			var gzip = new DeflateStream(buf, mode);
			gzip.Write(input, 0, input.Length);
			gzip.Close();
			return buf.ToArray();
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

		public static byte[] Decompress(byte[] buf)
		{
			return Compress(buf, CompressionMode.Decompress);
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
		public static string AddBOM(this string str)
		{
			if (Program.settings.useBOM)
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
	}


}
