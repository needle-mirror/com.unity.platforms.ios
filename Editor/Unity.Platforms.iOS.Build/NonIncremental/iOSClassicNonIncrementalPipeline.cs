using System.IO;
using Unity.Build;
using Unity.Build.Classic;
using Unity.Build.Common;
using Unity.BuildSystem.NativeProgramSupport;
using UnityEditor;

namespace Unity.Platforms.iOS.Build
{
    sealed class iOSClassicNonIncrementalPipeline : ClassicNonIncrementalPipelineBase
    {
        public override Platform Platform { get; } = new IosPlatform();
        protected override BuildTarget BuildTarget => BuildTarget.iOS;

        public override BuildStepCollection BuildSteps { get; } = new[]
        {
            typeof(SaveScenesAndAssetsStep),
            typeof(ApplyUnitySettingsStep),
            typeof(SwitchPlatfomStep),
            typeof(BuildPlayerStep),
            typeof(CopyAdditionallyProvidedFilesStep),
            typeof(iOSProduceArtifactStep)
        };

        protected override void PrepareContext(BuildContext context)
        {
            base.PrepareContext(context);

            var classicData = context.GetValue<ClassicSharedData>();

            classicData.StreamingAssetsDirectory =
                $"{context.GetOutputBuildDirectory()}/{context.GetComponentOrDefault<GeneralSettings>().ProductName}/Data/Raw";
        }

        protected override BoolResult OnCanRun(RunContext context)
        {
            var artifact = context.GetLastBuildArtifact<iOSBuildArtifact>();
            if (artifact == null)
            {
                return BoolResult.False($"Could not retrieve build artifact '{nameof(iOSBuildArtifact)}'.");
            }

            if (artifact.OutputTargetFile == null)
            {
                return BoolResult.False($"{nameof(iOSBuildArtifact.OutputTargetFile)} is null.");
            }

            // On iOS, the output target is a .app directory structure
            if (!Directory.Exists(artifact.OutputTargetFile.FullName))
            {
                return BoolResult.False($"Output target file '{artifact.OutputTargetFile.FullName}' not found.");
            }

            return BoolResult.True();
        }

        protected override RunResult OnRun(RunContext context)
        {
#if UNITY_IOS
            return context.Success(new iOSRunInstance());
#else
            return context.Failure($"Active Editor platform has to be set to iOS.");
#endif
        }
    }
}
