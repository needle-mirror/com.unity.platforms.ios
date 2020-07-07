using System;
using System.IO;
using Unity.Build.Common;
using Unity.Build.DotsRuntime;
using Unity.Build.iOS;

namespace Unity.Build.iOS.DotsRuntime
{
    public class iOSBuildTarget : BuildTarget
    {
        public override bool CanBuild => UnityEngine.Application.platform == UnityEngine.RuntimePlatform.OSXEditor;
        public override string DisplayName => "iOS";
        public override string BeeTargetName => "ios";
        public override string ExecutableExtension => "";
        public override string UnityPlatformName => nameof(UnityEditor.BuildTarget.iOS);
        public override bool UsesIL2CPP => true;

        public override Type[] UsedComponents { get; } =
        {
            typeof(GeneralSettings),
            typeof(BundleIdentifier),
            typeof(iOSSigningSettings),
            typeof(iOSTargetSettings),
            typeof(ScreenOrientations)
        };

        public override bool Run(FileInfo buildTarget)
        {
            UnityEditor.EditorUtility.RevealInFinder(buildTarget.FullName);
            return true;
        }

        public override ShellProcessOutput RunTestMode(string exeName, string workingDirPath, int timeout)
        {
            return new ShellProcessOutput
            {
                Succeeded = false,
                ExitCode = 0,
                FullOutput = "Test mode is not supported for iOS yet"
            };
        }

        public override void WriteBuildConfiguration(BuildContext context, string path)
        {
            var signingSettings = context.GetComponentOrDefault<iOSSigningSettings>();
            signingSettings.UpdateCodeSignIdentityValue();
            context.SetComponent(signingSettings);
            base.WriteBuildConfiguration(context, path);
        }
    }
}
