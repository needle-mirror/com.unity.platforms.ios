#if ENABLE_EXPERIMENTAL_INCREMENTAL_PIPELINE
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Bee.Core;
using Bee.Tools;
using Unity.Build;
using Unity.Build.Classic.Private.IncrementalClassicPipeline;
using Unity.Build.Common;
using UnityEngine;
#if UNITY_IOS
using System.IO;
using NiceIO;
using UnityEditor;
using UnityEditor.iOS;
using UnityEditor.iOS.Xcode;
using UnityEditor.iOS.Xcode.Extensions;
#endif

using UIOrientation = Unity.Build.Common.UIOrientation;

namespace Unity.Build.iOS.Classic
{
    sealed class GraphGenerateXcodeProject : BuildStepBase
    {
        static readonly Regex s_ModuleRegex = new Regex(@"(?<=)UnityEngine\.(\S+)Module\.dll(?=)", RegexOptions.Multiline);

        public override Type[] UsedComponents { get; } = { typeof(GeneralSettings), typeof(ScreenOrientations) };

        public override BuildResult Run(BuildContext context)
        {
#if UNITY_IOS
            var classicContext = context.GetValue<IncrementalClassicSharedData>();
            var buildDirectory = new NPath(context.GetOutputBuildDirectory()).MakeAbsolute();

            // Gather files from the iOS trampoline that we will use as our base project and copy them to the build directory
            // TODO: only copy the libraries needed. Not libil2cpp, baselib, etc for all variations
            var trampolinePath = classicContext.PlayerPackageDirectory.Combine("Trampoline");
            trampolinePath.Directories(true).Select(srcDir => buildDirectory.Combine(srcDir).EnsureDirectoryExists());
            var trampolineFilesToCopy = trampolinePath.Files(true).Where(path => path.Extension != "a" &&
                                                                path.FileName != "libiPhone-lib-il2cpp-dev.a" &&
                                                                path.RelativeTo(trampolinePath) != "Info.plist" &&
                                                                path.FileName != ".DS_Store" &&
                                                                path.FileName != "Preprocessor.h" &&
                                                                !path.HasDirectory("Unity-iPhone.xcodeproj"));

            foreach (var file in trampolineFilesToCopy)
            {
                CopyTool.Instance().Setup(buildDirectory.Combine(file.RelativeTo(trampolinePath)), file);
            }

            CopyTool.Instance().Setup($"{buildDirectory}/Libraries/libiPhone-lib.a", trampolinePath.Combine("Libraries/libiPhone-lib-il2cpp-dev.a"));

            // Finally copy the MapFileParser tool so managed stack traces can get symbolicated.
            CopyTool.Instance().Setup($"{buildDirectory}/MapFileParser", $"{EditorApplication.applicationContentsPath}/Tools/MapFileParser/MapFileParser");

            var productName = context.GetComponentOrDefault<GeneralSettings>().ProductName;
            var companyName = context.GetComponentOrDefault<GeneralSettings>().CompanyName;

            // Load up the Xcode project
            NPath projPath = $"{trampolinePath}/Unity-iPhone.xcodeproj/project.pbxproj";
            PBXProject proj = new PBXProject();
            proj.ReadFromString(projPath.ReadAllText());

            string mainTarget = proj.GetUnityMainTargetGuid();
            string frameworkTarget = proj.GetUnityFrameworkTargetGuid();
            string projectTarget = proj.ProjectGuid();
            string[] buildTargets = { frameworkTarget, mainTarget };
            string[] allProjectTargets = { frameworkTarget, mainTarget, projectTarget };

            // Remove source files and libraries that are no longer needed from the classic build.
            // And add a couple new ones for loading the engine api dynamically.
            proj.RemoveFile(proj.FindFileGuidByRealPath("Libraries/lib_burst_generated64.a"));

            // Now make sure that the libraries created by burst and bee are included in the UnityFramework target
            SetupDynamicLibraryInProject(proj, frameworkTarget, "Libraries/GameAssembly.dylib", buildDirectory, true, "6");
            SetupDynamicLibraryInProject(proj, mainTarget, "Libraries/lib_burst_generated.bundle", buildDirectory, false, "10");

            // Don't sign the framework when it's copied. It already gets signed and signing on copy actually strips out the signing on the Game Assembly dylib
            // Of course that means modifying how the framework is treated within the app targets embed frameworks copy files build phase.
            var fileGuid = proj.FindFileGuidByProjectPath("Products/UnityFramework.framework");
            var phase = proj.copyFiles[proj.GetCopyFilesBuildPhaseByTarget(mainTarget, "Embed Frameworks", "", "10")];
            var copyFileData = proj.FindFrameworkByFileGuid(phase, fileGuid);
            if (copyFileData != null)
            {
                copyFileData.codeSignOnCopy = false;
            }

            proj.SetBuildProperty(mainTarget, "PRODUCT_NAME", productName);
            proj.SetBuildProperty(mainTarget, "PRODUCT_BUNDLE_IDENTIFIER", companyName + ".${PRODUCT_NAME:rfc1123identifier}");

            proj.SetBuildProperty(projectTarget, "PRODUCT_NAME_APP", PlayerSettings.productName);

            proj.SetBuildProperty(buildTargets, "ARCHS", "arm64");
            proj.SetBuildProperty(buildTargets, "UNITY_RUNTIME_VERSION", Application.unityVersion);
            proj.SetBuildProperty(buildTargets, "UNITY_SCRIPTING_BACKEND", "il2cpp");

            // If we're running on Yamato override the signing credentials. Otherwise use what the user provided.
            if (Environment.GetEnvironmentVariable("YAMATO_PROJECT_ID") != null)
            {
                proj.SetBuildProperty(mainTarget, "CODE_SIGN_STYLE", "Manual");
                proj.SetBuildProperty(frameworkTarget, "CODE_SIGN_STYLE", "Manual");
                var appleTeamID = Environment.GetEnvironmentVariable("APPLE_SIGNING_TEAM_ID");
                if (appleTeamID != null)
                {
                    proj.SetBuildProperty(mainTarget, "DEVELOPMENT_TEAM", appleTeamID);
                    proj.SetBuildProperty(frameworkTarget, "DEVELOPMENT_TEAM", appleTeamID);
                }
                proj.SetBuildProperty(mainTarget, "PROVISIONING_PROFILE_SPECIFIER", "SLO-CI-CERT");
                proj.SetTargetAttributes("ProvisioningStyle", "Manual");
            }
            else
            {
                proj.SetBuildProperty(mainTarget, "DEVELOPMENT_TEAM", PlayerSettings.iOS.appleDeveloperTeamID);
                proj.SetBuildProperty(frameworkTarget, "DEVELOPMENT_TEAM", PlayerSettings.iOS.appleDeveloperTeamID);
            }
            proj.SetBuildProperty(buildTargets, "CODE_SIGN_IDENTITY[sdk=iphoneos*]", "iPhone Developer");

            proj.SetBuildProperty(allProjectTargets, "SDKROOT", "iphoneos");
            proj.SetBuildProperty(allProjectTargets, "SUPPORTED_PLATFORMS", "iphoneos");

            // This stuff is lifted from the iOS build post processor.
            // We may need to revisit it to implement only what is necessary or add more from the post processor.
            string [] cflagsToRemove = { "-mno-thumb" };

            proj.UpdateBuildProperty(mainTarget, "OTHER_CFLAGS", null, cflagsToRemove);
            proj.UpdateBuildProperty(frameworkTarget, "OTHER_CFLAGS", null, cflagsToRemove);

            NPath classRegistrationTarget =
                $"{buildDirectory}/Classes/Native/UnityClassRegistration.cpp";
            WriteUnityClassRegistration(context, classRegistrationTarget);
            proj.AddFile(classRegistrationTarget.ToString(), "Classes/Native/UnityClassRegistration.cpp");
            proj.AddFileToBuild(frameworkTarget, proj.FindFileGuidByRealPath(classRegistrationTarget.ToString()));

            NPath icallRegistrationTarget =
                $"{buildDirectory}/Classes/Native/UnityICallRegistration.cpp";
            GenerateInternalCallSummaryFile(context, icallRegistrationTarget);
            proj.AddFile(icallRegistrationTarget.ToString(), "Classes/Native/UnityICallRegistration.cpp");
            proj.AddFileToBuild(frameworkTarget, proj.FindFileGuidByRealPath(icallRegistrationTarget.ToString()));

            // Replace the OpenGLES framework with Metal
            proj.RemoveFrameworkFromProject(frameworkTarget, "OpenGLES.framework");
            proj.AddFrameworkToProject(frameworkTarget, "Metal.framework", weak: false);

            UpdateDefinesInFile($"{trampolinePath}/Classes/Preprocessor.h".ToString(), $"{buildDirectory}/Classes/Preprocessor.h", new Dictionary<string, bool>()
            {
                { "PLATFORM_IOS", true },
                { "UNITY_USES_GLES", false },
                { "UNITY_DEVELOPER_BUILD", true },
            });

            UpdateInfoPlist(context, $"{trampolinePath}/Info.plist", $"{buildDirectory}/Info.plist");

            // Save the changes to disk
            Backend.Current.AddWriteTextAction($"{buildDirectory}/Unity-iPhone.xcodeproj/project.pbxproj", proj.WriteToString());

            // Generate dummy precompiled headers for il2cpp.
            Backend.Current.AddWriteTextAction($"{buildDirectory}/Classes/pch-c.h", string.Empty);
            Backend.Current.AddWriteTextAction($"{buildDirectory}/Classes/pch-cpp.hpp", string.Empty);
#endif
            return context.Success();
        }

#if UNITY_IOS
        void SetupDynamicLibraryInProject(PBXProject proj, string target, NPath outputFile, NPath buildDirectory, bool link, string subFolderSpec)
        {
            NPath destination = $"{buildDirectory}/{outputFile}";
            var fileGuid = proj.AddFile(destination.ToString(), destination.RelativeTo(buildDirectory).ToString());

            if (link)
            {
                var linkPhase = proj.GetFrameworksBuildPhaseByTarget(target);
                proj.AddFileToBuildSection(target, linkPhase, fileGuid);
            }

            proj.AddFileToCopyFilesWithSubfolder(target, fileGuid, null, subFolderSpec);
        }

        // The following functions are filling in functionality that the iOS build postprocessor does for classic builds.

        // Replaces the first C++ macro with the given name in the source file. Only changes
        // single-line macro declarations, if multi-line macro declaration is detected, the
        // function returns without changing it. Macro name must be a valid C++ identifier.
        void ReplaceCppMacro(string[] lines, string name, string newValue)
        {
            var matchRegex = new Regex(@"^.*#\s*define\s+" + name);
            var replaceRegex = new Regex(@"^.*#\s*define\s+" + name + @"(:?|\s|\s.*[^\\])$");
            for (int i = 0; i < lines.Length; i++)
            {
                if (matchRegex.Match(lines[i]).Success)
                    lines[i] = replaceRegex.Replace(lines[i], "#define " + name + " " + newValue);
            }
        }

        void UpdateDefinesInFile(string srcFile, NPath dstFile, Dictionary<string, bool> valuesToUpdate)
        {
            var src = File.ReadAllLines(srcFile);
            var copy = (string[])src.Clone();

            foreach (var kvp in valuesToUpdate)
                ReplaceCppMacro(copy, kvp.Key, kvp.Value ? "1" : "0");

            if (!copy.SequenceEqual(src))
                File.WriteAllLines(srcFile, copy);

            Backend.Current.AddWriteTextAction(dstFile, string.Join("\n", copy));
        }

        // Necessary class registration for modules converted to c++
        void WriteUnityClassRegistration(BuildContext context, NPath outputPath)
        {
            var classicContext = context.GetValue<IncrementalClassicSharedData>();
            var dlls = classicContext.UnityEngineAssembliesDirectory.Files()
                .Where(f => f.Extension == "dll")
                .Select(f => f.FileName.ToString()).SeparateWith("\n");

            var text = $@"extern ""C"" void RegisterStaticallyLinkedModulesGranular()
{{
{s_ModuleRegex.Matches(dlls).Cast<Match>()
                .Where(m => m.Success)
                .Select(f => f.Groups[1].Captures[0].Value)
                .Select(name => $@"    void RegisterModule_{name}(); RegisterModule_{name}();")
                .SeparateWith("\n")}
}}

template <typename T> void RegisterUnityClass(const char*);
template <typename T> void RegisterStrippedType(int, const char*, const char*);

void InvokeRegisterStaticallyLinkedModuleClasses()
{{
    void RegisterStaticallyLinkedModuleClasses();
    RegisterStaticallyLinkedModuleClasses();
}}

void RegisterAllClasses()
{{
    void RegisterAllClassesGranular();
    RegisterAllClassesGranular();
}}
";

            NPath classRegistrationOutput = $"{Configuration.RootArtifactsPath}/iosclassregistration/UnityClassRegistration.cpp";
            Backend.Current.AddWriteTextAction(classRegistrationOutput, text, "Generating Class Registration");

            CopyTool.Instance().Setup(outputPath, classRegistrationOutput);
        }

        void GenerateInternalCallSummaryFile(BuildContext context, NPath outputPath)
        {
            var dllDir = context.GetValue<IncrementalClassicSharedData>().UnityEngineAssembliesDirectory;

            var dlls = dllDir.Files().Where(p => p.ToString().EndsWith("Module.dll") || p.ToString().Contains("UnityEngine.dll"));
            if (dlls.Empty())
                return;

            var args = string.Format("{0} -output=\"{1}\" -summary=\"{2}\" -assembly=\"{3}\"",
                $"{EditorApplication.applicationContentsPath}/Tools/InternalCallRegistrationWriter/InternalCallRegistrationWriter.exe",
                outputPath,
                $"{context.GetOutputBuildDirectory()}/ICallSummary.txt",
                dlls.Aggregate((dllArg, next) => dllArg + ";" + next)
            );

            Backend.Current.AddAction("Generating Internal Call Registration",
                new [] { outputPath },
                dlls.ToArray(),
                $"{EditorApplication.applicationContentsPath}/MonoBleedingEdge/bin/mono",
                args.Split(' ').ToArray());
        }

        static string GetStatusBarStyle(iOSStatusBarStyle style)
        {
            switch (style)
            {
                case iOSStatusBarStyle.Default:             return "UIStatusBarStyleDefault";
                case iOSStatusBarStyle.LightContent:        return "UIStatusBarStyleLightContent";
            }
            return "";
        }

        static List<string> GetAvailableOrientations(ScreenOrientations screenOrientations)
        {
            var res = new List<string>();
            UIOrientation orient = screenOrientations.DefaultOrientation;

            bool autorotation = orient == UIOrientation.AutoRotation;
     
            if (orient == UIOrientation.Portrait || (autorotation && screenOrientations.AllowAutoRotateToPortrait))
                res.Add("UIInterfaceOrientationPortrait");

            if (orient == UIOrientation.PortraitUpsideDown || (autorotation && screenOrientations.AllowAutoRotateToReversePortrait))
                res.Add("UIInterfaceOrientationPortraitUpsideDown");

            if (orient == UIOrientation.LandscapeLeft || (autorotation && screenOrientations.AllowAutoRotateToLandscape))
                res.Add("UIInterfaceOrientationLandscapeLeft");

            if (orient == UIOrientation.LandscapeRight || (autorotation && screenOrientations.AllowAutoRotateToReverseLandscape))
                res.Add("UIInterfaceOrientationLandscapeRight");

            return res;
        }

        void UpdateInfoPlist(BuildContext context, NPath inputPlist, NPath outputPath)
        {
            string text = inputPlist.ReadAllText();
            string displayName = PlayerSettings.iOS.applicationDisplayName;

            var data = new PlistUpdater.CustomData();
            data.bundleVersion                  = PlayerSettings.bundleVersion;
            data.buildNumber                    = PlayerSettings.iOS.buildNumber;
            data.bundleDisplayName              = string.IsNullOrEmpty(displayName) ? "${PRODUCT_NAME}" : displayName;
            data.availableOrientationSet        = GetAvailableOrientations(context.GetComponentOrDefault<ScreenOrientations>());
            data.iPhoneLaunchStoryboardName     = "LaunchScreen-iPhone";
            data.iPadLaunchStoryboardName       = "LaunchScreen-iPad";
            data.isIconPrerendered              = PlayerSettings.iOS.prerenderedIcon;
            data.isPersistentWifiRequired       = PlayerSettings.iOS.requiresPersistentWiFi;
            data.requiresFullScreen             = PlayerSettings.iOS.requiresFullScreen;
            data.isStatusBarHidden              = PlayerSettings.statusBarHidden;
            data.statusBarStyle                 = GetStatusBarStyle(PlayerSettings.iOS.statusBarStyle);
            data.backgroundModes                = (PlayerSettings.iOS.appInBackgroundBehavior == iOSAppInBackgroundBehavior.Custom ? PlayerSettings.iOS.backgroundModes : iOSBackgroundMode.None);
            data.loadingActivityIndicatorStyle  = (int)PlayerSettings.iOS.showActivityIndicatorOnLoading;
            data.cameraUsageDescription         = PlayerSettings.iOS.cameraUsageDescription;
            data.locationUsageDescription       = PlayerSettings.iOS.locationUsageDescription;
            data.microphoneUsageDescription     = PlayerSettings.iOS.microphoneUsageDescription;
            data.allowHTTP                      = PlayerSettings.iOS.allowHTTPDownload;
            data.supportedURLSchemes            = PlayerSettings.iOS.iOSUrlSchemes.ToList();
            data.tvOSRequireExtendedGameController = PlayerSettings.tvOS.requireExtendedGameController;
            data.isAppleTV                      = false;
            data.requiresARKitSupport = false;//PlayerSettings.iOS.requiresARKitSupport;
            data.enableProMotion = false;//PlayerSettings.iOS.appleEnableProMotion;
            data.allowMixedLocalizations        = true; // required to get actual device locale on iOS 11+
            data.requireES3 = false;
            data.requireMetal = true;

            try
            {
                text = PlistUpdater.UpdateString(text, data);
            }
            catch (Exception e)
            {
                // Incorrect Info.Plist is not a fatal error. Log and ignore
                Debug.LogException(e);
                return;
            }

            NPath plistOutput = $"{Configuration.RootArtifactsPath}/iosinfoplist/Info.plist";
            Backend.Current.AddWriteTextAction(plistOutput, text, "Updating Info.plist");

            CopyTool.Instance().Setup(outputPath, plistOutput);
        }
#endif
    }
}
#endif
