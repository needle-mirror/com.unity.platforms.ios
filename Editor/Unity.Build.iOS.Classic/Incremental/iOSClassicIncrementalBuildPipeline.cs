#if ENABLE_EXPERIMENTAL_INCREMENTAL_PIPELINE
using System;
using System.Linq;
using Bee.Core;
using Bee.NativeProgramSupport;
using Bee.Toolchain.GNU;
using Bee.Toolchain.IOS;
using Bee.Toolchain.Xcode;
using NiceIO;
using Unity.Build;
using Unity.Build.Internals;
using Unity.Build.Classic;
using Unity.Build.Classic.Private;
using Unity.Build.Classic.Private.IncrementalClassicPipeline;
using Unity.Build.Common;
using UnityEditor;

namespace Unity.Build.iOS.Classic
{
    class iOSClassicIncrementalBuildPipeline : ClassicIncrementalPipelineBase
    {
        public override Platform Platform { get; } = new IosPlatform();
        protected override BuildTarget BuildTarget => BuildTarget.iOS;

        public override BuildStepCollection BuildSteps { get; } = new[]
        {
            typeof(SetupCopiesFromSlimPlayerBuild),
            typeof(GraphCopyDefaultResources),
            typeof(GraphSetupCodeGenerationStep),
            typeof(GraphSetupIl2Cpp),
            typeof(GraphSetupNativePlugins),
            typeof(GraphSetupPlayerFiles),
            typeof(SetupAdditionallyProvidedFiles),
            typeof(GraphGenerateXcodeProject),
            typeof(GraphBuildXcodeProject)
        };

        class iOSIL2CPPSupport : IL2CPPPlatformBeeSupport
        {
            public override void ProvideLibIl2CppProgramSettings(NativeProgram program)
            {
                program.CompilerSettingsForIosOrTvos().Add(s => s.WithEmbedBitcode(true));
            }

            public override void ProvideNativeProgramSettings(NativeProgram program)
            {
                program.CompilerSettingsForIosOrTvos().Add(s => s.WithEmbedBitcode(true));
                program.DynamicLinkerSettingsForIosOrTvos().Add(s => s.WithInstallName("@loader_path/GameAssembly.dylib"));
            }

        }

        protected override void PrepareContext(BuildContext context)
        {
            base.PrepareContext(context);

            context.SetValue(new iOSIL2CPPSupport());

            var classicContext = context.GetValue<IncrementalClassicSharedData>();
            var buildDirectory = new NPath(context.GetOutputBuildDirectory()).MakeAbsolute();

            classicContext.DataDeployDirectory = $"{buildDirectory}/Data";

            var classicData = context.GetValue<ClassicSharedData>();

            classicData.StreamingAssetsDirectory = $"{classicContext.DataDeployDirectory}/Raw".ToString();
            classicContext.VariationDirectory =
                $"{classicContext.PlayerPackageDirectory}/Variations/il2cpp/Developmentarm64_managed";
            classicContext.UnityEngineAssembliesDirectory =
                $"{classicContext.PlayerPackageDirectory}/Variations/il2cpp/Managed";
            classicContext.IL2CPPDataDirectory = $"{classicContext.DataDeployDirectory}/Managed";

            var burstSettings = context.GetOrCreateValue<BurstSettings>();
            burstSettings.BurstGranularity = BurstGranularity.One;

            var iosSdk = IOSSdk.LocatorArm64.UserDefaultOrLatest;
            var toolchain = new IOSToolchain(iosSdk);
            classicContext.Architectures.Add(Architecture.Arm64,
                new ClassicBuildArchitectureData()
                {
                    DynamicLibraryDeployDirectory = $"{buildDirectory}/Libraries",
                    IL2CPPLibraryDirectory = $"{buildDirectory}/Libraries",
                    BurstTarget = "ARMV8A_AARCH64",
                    ToolChain = toolchain,
                    NativeProgramFormat = toolchain.DynamicLibraryFormat
                });

            classicContext.LibraryDeployDirectory = classicContext.Architectures[Architecture.Arm64].DynamicLibraryDeployDirectory;
        }

        protected override CleanResult OnClean(CleanContext context)
        {
            var buildType = context.GetComponentOrDefault<ClassicBuildProfile>().Configuration;
            bool isDevelopment = buildType == BuildType.Debug || buildType == BuildType.Develop;
            var playbackEngineDirectory = new NPath(UnityEditor.BuildPipeline.GetPlaybackEngineDirectory(BuildTarget, isDevelopment ? BuildOptions.Development : BuildOptions.None));

            if (context.HasComponent<InstallInBuildFolder>())
            {
                NPath sourceBuildDirectory = $"{playbackEngineDirectory}/SourceBuild/{context.BuildConfigurationName}";

                if (sourceBuildDirectory.DirectoryExists())
                    sourceBuildDirectory.Delete();
            }
            return base.OnClean(context);
        }

        protected override BoolResult OnCanRun(RunContext context)
        {
#if UNITY_IOS
            return BoolResult.True();
#else
            return BoolResult.False("Active Editor platform has to be set to iOS.");
#endif
        }

        protected override RunResult OnRun(RunContext context)
        {
#if UNITY_IOS
            try
            {
                var runTargets = context.RunTargets;

                // adhoc discovery
                // TODO will be removed with pram async discovery
                if (!runTargets.Any())
                {
                    runTargets = new Pram().Discover(new [] {
                        "appledevice"
                    });

                    // if any devices were found, only pick first
                    if (runTargets.Any())
                        runTargets = new[] {runTargets.First()};
                }

                if (!runTargets.Any())
                    throw new Exception("No iOS devices available");

                var productName = context.GetComponentOrDefault<GeneralSettings>().ProductName;
                var buildDirectory = new NPath(context.GetOutputBuildDirectory());
                var buildType = context.GetComponentOrDefault<ClassicBuildProfile>().Configuration;

                var config = "ReleaseForRunning";
                if (buildType == BuildType.Debug)
                    config = "Debug";

                NPath appBundlePath =
                    $"{buildDirectory}/xcode_DerivedData/Build/Products/{config}-iphoneos/{productName}.app";

                var applicationId =
                    $"{context.GetComponentOrDefault<GeneralSettings>().CompanyName}.{context.GetComponentOrDefault<GeneralSettings>().ProductName}";
                foreach (var device in runTargets)
                {
                    EditorUtility.DisplayProgressBar("Installing Application", $"Installing {applicationId} to {device.DisplayName}", 0.2f);
                    device.Deploy(applicationId, appBundlePath.MakeAbsolute().ToString());

                    EditorUtility.DisplayProgressBar("Starting Application", $"Starting {applicationId} on {device.DisplayName}", 0.8f);
                    device.ForceStop(applicationId);
                    device.Start(applicationId);
                }
            }
            catch (Exception ex)
            {
                return context.Failure(ex);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            return context.Success();
#else
            return context.Failure($"Active Editor platform has to be set to iOS.");
#endif
        }
    }
}
#endif
