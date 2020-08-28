using System;
using System.IO;
using System.Linq;
using Unity.Build.Common;
using Unity.Build.DotsRuntime;
using Unity.Build.Internals;
using UnityEngine;

namespace Unity.Build.iOS.DotsRuntime
{
    public class iOSBuildTarget : BuildTarget
    {
        protected static readonly Texture2D s_Icon = LoadIcon("Icons", "BuildSettings.iPhone");

        public override bool CanBuild => UnityEngine.Application.platform == UnityEngine.RuntimePlatform.OSXEditor;
        public override bool CanRun => !ExportProject && TargetSettings?.SdkVersion == iOSSdkVersion.DeviceSDK;
        public override string DisplayName => "iOS";
        public override string BeeTargetName => "ios";
        public override string ExecutableExtension => ExportProject ? "" : ".app";
        public override string UnityPlatformName => nameof(UnityEditor.BuildTarget.iOS);
        public override bool UsesIL2CPP => true;
        public override Texture2D Icon => s_Icon;

        public override Type[] UsedComponents { get; } =
        {
            typeof(GeneralSettings),
            typeof(ApplicationIdentifier),
            typeof(iOSBuildNumber),
            typeof(iOSSigningSettings),
            typeof(iOSExportProject),
            typeof(iOSTargetSettings),
            typeof(ScreenOrientations),
            typeof(iOSIcons)
        };

        ApplicationIdentifier Identifier { get; set; }
        iOSTargetSettings TargetSettings { get; set; }
        bool ExportProject { get; set; }

        public override bool Run(FileInfo buildTarget)
        {
            try
            {
                var runTargets = new Pram().Discover(new[] { "appledevice" });

                // if any devices were found, only pick first
                if (runTargets.Any())
                    runTargets = new[] { runTargets.First() };

                if (!runTargets.Any())
                    throw new Exception("No iOS devices available");

                var applicationId = Identifier?.PackageName;
                foreach (var device in runTargets)
                {
                    UnityEditor.EditorUtility.DisplayProgressBar("Installing Application", $"Installing {applicationId} to {device.DisplayName}", 0.2f);
                    device.Deploy(applicationId, buildTarget.FullName);

                    UnityEditor.EditorUtility.DisplayProgressBar("Starting Application", $"Starting {applicationId} on {device.DisplayName}", 0.8f);
                    device.ForceStop(applicationId);
                    device.Start(applicationId);
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError(ex.ToString());
                return false;
            }
            return true;
        }

        internal override ShellProcessOutput RunTestMode(string exeName, string workingDirPath, int timeout)
        {
            //TODO "app-start-attached" is not implemented yet in Pram for Apple devices (ios-deploy can gather application log from device)
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
            Identifier = context.GetComponentOrDefault<ApplicationIdentifier>();
            TargetSettings = context.GetComponentOrDefault<iOSTargetSettings>();
            ExportProject = context.HasComponent<iOSExportProject>();
            base.WriteBuildConfiguration(context, path);
        }
    }
}
