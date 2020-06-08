#if ENABLE_EXPERIMENTAL_INCREMENTAL_PIPELINE
using Bee.Core;
using NiceIO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Bee.Toolchain.IOS;
using Bee.Toolchain.Xcode;
using Unity.Build;
using Unity.Build.Classic;
using Unity.Build.Classic.Private.IncrementalClassicPipeline;
using Unity.Build.Common;
using Unity.BuildSystem.NativeProgramSupport;

namespace Unity.Build.iOS.Classic
{
    sealed class GraphBuildXcodeProject : BuildStepBase
    {
        public override Type[] UsedComponents { get; } = { typeof(GeneralSettings), typeof(ClassicBuildProfile) };

        public override BuildResult Run(BuildContext context)
        {
            var productName = context.GetComponentOrDefault<GeneralSettings>().ProductName;
            var buildDirectory = new NPath(context.GetOutputBuildDirectory()).MakeAbsolute();
            var buildType = context.GetComponentOrDefault<ClassicBuildProfile>().Configuration;

            // Currently burst does not support building a dylib for iOS/tvOS.
            // So we relink each one into a dylib.
            var burstBundlePath = ConvertBurstStaticLibToDynamic(context);

            var config = "ReleaseForRunning";
            if (buildType == BuildType.Debug)
                config = "Debug";

            NPath output = $"{buildDirectory}/xcode_DerivedData/Build/Products/{config}-iphoneos/{productName}.app";

            // Take inputs from the linker and il2cpp
            NPath [] inputFiles =
            {
                burstBundlePath,
                $"{buildDirectory}/Unity-iPhone.xcodeproj/project.pbxproj",
                $"{buildDirectory}/Classes/Native/UnityClassRegistration.cpp",
                $"{buildDirectory}/Classes/Preprocessor.h",
                $"{buildDirectory}/Info.plist",
                $"{buildDirectory}/Libraries/GameAssembly.dylib"
            };

            // Run Xcodebuild
            Backend.Current.AddAction("Build Xcode Project",
                Array.Empty<NPath>(),
                inputFiles,
                "/usr/bin/xcodebuild",
                new[]
                {
                    "-scheme Unity-iPhone",
                    $"-configuration {config}",
                    "-allowProvisioningUpdates",
                    "-allowProvisioningDeviceRegistration",
                    $"-derivedDataPath {buildDirectory.Combine("xcode_DerivedData").InQuotes()}",
                    $"-project {buildDirectory.Combine("Unity-iPhone.xcodeproj").InQuotes()}"
                }, targetDirectories: new[] { output }, allowUnwrittenOutputFiles: true);

            return context.Success();
        }

        NPath ConvertBurstStaticLibToDynamic(BuildContext context)
        {
            var classicContext = context.GetValue<IncrementalClassicSharedData>();

            // We really want burst as a dylib but it doesn't support that right now so...
            // Re-link the static lib as a dynamic lib
            var burstLib = Configuration.RootArtifactsPath.Combine("bcl", "wholeprogram", "wholeprogram.a");
            var nativeProgram = new NativeProgram("lib_burst_generated");
            nativeProgram.Libraries.Add(c =>
                new StaticLibrary(burstLib));
            var iosSdk = IOSSdk.LocatorArm64.UserDefaultOrLatest;
            var toolchain = new IOSToolchain(iosSdk);
            var npc = new NativeProgramConfiguration(CodeGen.Release, toolchain, false);

            var format = toolchain.DynamicLibraryFormat;
            nativeProgram.DynamicLinkerSettingsForIosOrTvos().Add(s => s.WithAllLoad(true));
            var builtNativeProgram = nativeProgram.SetupSpecificConfiguration(npc, format);
            var dynLibDeployDir = classicContext.Architectures[Architecture.Arm64].DynamicLibraryDeployDirectory
                .MakeAbsolute();
            builtNativeProgram.DeployTo(dynLibDeployDir);

            // Now that we have built and deployed we need to rename it. The iOS plugin architecture has .bundle hardcoded.
            return CopyTool.Instance().Setup(dynLibDeployDir.Combine("lib_burst_generated.bundle"), dynLibDeployDir.Combine("lib_burst_generated.dylib"));
        }
    }
}
#endif
