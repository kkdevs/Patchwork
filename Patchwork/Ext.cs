using MessagePack;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace Patchwork
{
	public static class Ext
	{
		public static IEnumerable<MemberInfo> GetVars(this Type t)
		{
			foreach (var v in t.GetFields())
				yield return v;
			foreach (var gs in t.GetProperties())
				yield return gs;
		}
		public static Type GetVarType(this MemberInfo m)
		{
			var f = m as FieldInfo;
			if (f != null)
				return f.FieldType;
			var p = m as PropertyInfo;
			if (p != null)
				return p.PropertyType;
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

		public static bool GetAttr<T>(this MemberInfo m, ref T ret) where T : class
		{
			foreach (var attr in m.GetCustomAttributes(true))
			{
				if (attr is T)
				{
					ret = attr as T;
					return true;
				}
			}
			return false;
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
				return ass.GetExportedTypes();
			} catch {
				try
				{
					return ass.GetTypes();
				} catch
				{
					return new Type[] { };
				}
			}
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
			return ct.GetMethods().First(t => t.Name == "Serialize" && t.GetParameters().Count() == 1);
		}


		public static byte[] SerializeObject(object o, bool lz4)
		{
			var ser = lz4 ? lz4serialize : serialize;
			return ser.MakeGenericMethod(o.GetType()).Invoke(null, new object[] { o }) as byte[];
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
		public static T ScriptComponent<T>(this GameObject go) where T : MonoBehaviour
		{
			var name = typeof(T).Name;
			if (Script.Components != null && Program.settings.CheckComponent(name) && Script.Components.TryGetValue(name, out Type t))
			{
				Debug.Log($"Spawning component {t.Name}");
				return go.AddComponent(t) as T;
			}
			return go.AddComponent(typeof(T)) as T;
		}
	}


}
