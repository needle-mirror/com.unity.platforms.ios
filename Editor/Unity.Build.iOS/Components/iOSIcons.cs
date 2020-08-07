using Unity.Build.Classic;
using Unity.Properties;
using Unity.Serialization;
using UnityEditor;

namespace Unity.Build.iOS
{
    internal sealed partial class iOSIcons : IBuildComponent
    {
        public iOSIcons() {}

        public iOSIcons(iOSIcons from)
        {
            iPhone2x = from.iPhone2x;
            iPhone3x = from.iPhone3x;
            iPad2x = from.iPad2x;
            iPadPro2x = from.iPadPro2x;
            AppStore = from.AppStore;
        }
    }
}
