#if ENABLE_EXPERIMENTAL_INCREMENTAL_PIPELINE
using System.Collections.Generic;
using System.IO;
using NiceIO;
using Unity.Build.Classic.Private;

namespace Unity.Build.iOS.Classic
{
    sealed class PramiOSPlugin : PramPlatformPlugin
    {
        public override string[] Providers { get; } = {"appledevice"};
        public override NPath PlatformAssemblyLoadPath
        {
            get { return Path.GetFullPath("Packages/com.unity.platforms.ios/Editor/Unity.Build.iOS.Classic/pram~"); }
        }

        public override IReadOnlyDictionary<string, string> Environment { get; } = new Dictionary<string, string> ();
    }
}
#endif
