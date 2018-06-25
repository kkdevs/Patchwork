using Patchwork;
using System;
using System.Collections.Generic;
using System.Reflection;

public class EzHook
{
	/// <summary>
	/// Ghetto, IL-free harmony.
	/// </summary>
	public static void Apply(Type to, Type from)
	{
		var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
		foreach (var mfrom in from.GetMethods(flags))
		{
			if (mfrom.DeclaringType != from) continue;
			var mto = to.GetMethod(mfrom.Name, flags);
			if (mto == null) continue;
			Trace.Log(mto.Name);
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
