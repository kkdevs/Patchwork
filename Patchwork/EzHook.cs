using System;
using System.Collections.Generic;
using System.Reflection;

public class EzHook<T>
{
	/// <summary>
	/// Ghetto, IL-free harmony.
	/// </summary>
	public static void ApplyTo(Type a)
	{
		var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
		foreach (var bm in typeof(T).GetMethods(flags))
		{
			if (bm.DeclaringType != typeof(T)) continue;
			var am = a.GetMethod(bm.Name, flags);
			if (am == null) continue;
			unsafe
			{
				var ap = (byte*)am.MethodHandle.GetFunctionPointer();
				*ap++ = 0xff;
				*ap++ = 0x25;
				*(int*)ap = IntPtr.Size == 4 ? ((int)ap + 4) : 0;
				*(IntPtr*)(ap + 4) = bm.MethodHandle.GetFunctionPointer();
			}
		}
	}
}

