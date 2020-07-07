using Unity.Properties;

namespace Unity.Build.iOS
{
    internal sealed partial class BundleIdentifier
    {
        string m_BundleName;

        [CreateProperty]
        public string BundleName
        {
            get => !string.IsNullOrEmpty(m_BundleName) ? m_BundleName : "com.unity.DefaultPackage";
            set => m_BundleName = value;
        }
    }
}
