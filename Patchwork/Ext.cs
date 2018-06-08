using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

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
	}


}
