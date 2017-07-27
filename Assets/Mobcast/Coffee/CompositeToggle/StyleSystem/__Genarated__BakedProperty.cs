using System.Collections.Generic;

namespace Mobcast.Coffee.Toggles
{
	public static class BakedProperty
	{
		public delegate void PropertySetter(UnityEngine.Object target,IParameterList args,int index);

		public static Dictionary<string, PropertySetter> baked = new Dictionary<string, PropertySetter>()
		{
			// BAKED PROPERTIES START
			// BAKED PROPERTIES END
		};
	}
}