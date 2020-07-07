using Unity.Build.Classic;
using Unity.Properties;
using Unity.Serialization;
using UnityEditor;

namespace Unity.Build.iOS
{
    internal sealed partial class BundleIdentifier : IBuildComponent, ICustomBuildComponentConstructor
    {
        void ICustomBuildComponentConstructor.Construct(BuildConfiguration.ReadOnly config)
        {
            var group = config.GetBuildTargetGroup();
            if (group == BuildTargetGroup.Unknown)
                return;

            m_BundleName = PlayerSettings.GetApplicationIdentifier(group);
        }
    }
}
