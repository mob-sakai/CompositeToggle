using UnityEditor;

namespace Mobcast.Coffee.Toggles
{
	public static class ExportPackage
	{
		const string kPackageName = "CompositeToggle.unitypackage";
		static readonly string[] kAssetPathes = {
			"Assets/Mobcast/Coffee/CompositeToggle",
		};

		[MenuItem ("Export Package/" + kPackageName)]
		[InitializeOnLoadMethod]
		static void Export ()
		{
			if (EditorApplication.isPlayingOrWillChangePlaymode)
				return;
			
			AssetDatabase.ExportPackage (kAssetPathes, kPackageName, ExportPackageOptions.Recurse | ExportPackageOptions.Default);
			UnityEngine.Debug.Log ("Export successfully : " + kPackageName);
		}
	}
}