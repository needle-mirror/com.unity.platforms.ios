#if ENABLE_EXPERIMENTAL_INCREMENTAL_PIPELINE
using System.Collections.Generic;
using System.IO;
using NiceIO;
using Unity.Build.Classic.Private;

namespace Unity.Platforms.iOS.Build
{
    sealed class PramiOSPlugin : PramPlatformPlugin
    {
        public override string[] Providers { get; } = {"appledevice"};
        public override NPath PlatformAssemblyLoadPath
        {
            get { return Path.GetFullPath("Packages/com.unity.platforms.ios/Editor/Unity.Platforms.iOS.Build/pram~"); }
        }

        public override IReadOnlyDictionary<string, string> Environment { get; } = new Dictionary<string, string> ();
    }
}
#endif
