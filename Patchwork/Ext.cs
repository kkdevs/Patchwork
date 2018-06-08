using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Patchwork
{
	public static class Ext
	{
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
