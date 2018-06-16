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
