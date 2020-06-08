using System.IO;
using Unity.Build.Classic;
using Unity.Build.Common;
using Unity.Build.Editor;
using Unity.BuildSystem.NativeProgramSupport;
using UnityEditor;

namespace Unity.Build.iOS.Classic
{
    static class iOSMenuItem
    {
        const string k_CreateBuildConfigurationAssetClassic = BuildConfigurationMenuItem.k_BuildConfigurationMenu + "iOS Classic Build Configuration";

        [MenuItem(k_CreateBuildConfigurationAssetClassic, true)]
        static bool CreateBuildConfigurationAssetClassicValidation()
        {
            return Directory.Exists(AssetDatabase.GetAssetPath(Selection.activeObject));
        }

        [MenuItem(k_CreateBuildConfigurationAssetClassic)]
        static void CreateBuildConfigurationAsset()
        {
            Selection.activeObject = BuildConfigurationMenuItem.CreateAssetInActiveDirectory(
                "iOSClassic",
                new GeneralSettings(),
                new SceneList(),
                new ClassicBuildProfile { Platform = new IosPlatform() });
        }
    }
}
