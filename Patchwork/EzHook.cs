using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

public class EzHook
{
	[System.AttributeUsage(AttributeTargets.All)]
	public class DontMirror : Attribute { }
	[System.AttributeUsage(AttributeTargets.All)]
	public class DontHook : Attribute { }

	/// <summary>
	/// Ghetto, IL-free harmony.
	/// </summary>
	public static void Apply(Type to, Type from)
	{
		Debug.Log("Patching ",to, "in", to?.Assembly,"with",from,from?.Assembly);
		if (to == null || from == null) return;
		var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
		foreach (var mfrom in from.GetMethods(flags))
		{
			if (mfrom.DeclaringType != from) continue;
			if (mfrom.IsConstructor) continue;
			if (mfrom.GetCustomAttributes(true).Any(x => x is DontHook)) continue;
			Debug.Log($"{mfrom.Name}");
			/*var mto = to.GetMethod(mfrom.Name, flags, null, mfrom.GetParameters().Select(x => x.ParameterType).ToArray(), null);
			if (mto == null)
			{
				Debug.Log("no target; skipping");
				continue;
			}*/
			var mto = to.GetMethod(mfrom.Name, flags);
			if (mto == null)
				continue;
			if (mto.IsStatic && mto.Name.StartsWith("get_"))
			{
				var fprop = from.GetProperty(mto.Name.Substring(4), flags);
				var tprop = to.GetProperty(mto.Name.Substring(4), flags);
				if (fprop!=null && tprop!=null && fprop.CanRead && tprop.CanWrite && tprop.PropertyType.IsAssignableFrom(fprop.PropertyType) && !fprop.GetCustomAttributes(true).Any(x=>x is DontMirror))
					tprop.SetValue(null, fprop.GetValue(null));
			}

			unsafe
			{
				var ap = (byte*)mto.MethodHandle.GetFunctionPointer();
				*ap++ = 0xff;
				*ap++ = 0x25;
				*(int*)ap = IntPtr.Size == 4 ? ((int)ap + 4) : 0;
				*(IntPtr*)(ap + 4) = mfrom.MethodHandle.GetFunctionPointer();
			}
		}
	}
}

public class EzHook<T>
{
	public static void ApplyTo(Type to)
	{
		EzHook.Apply(to, typeof(T));
	}
}
