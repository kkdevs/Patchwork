using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Patchwork
{
	public static class Ext
	{
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
