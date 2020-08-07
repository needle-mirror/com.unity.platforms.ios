using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using Bee.Core;
using Bee.DotNet;
using Bee.Stevedore;
using Bee.Toolchain.Xcode;
using Bee.Toolchain.Extension;
using Bee.BuildTools;
using Newtonsoft.Json.Linq;
using NiceIO;
using UnityEditor.iOS.Xcode.Custom;
using UnityEditor.iOS.Xcode.Custom.Extensions;
using Unity.Build.Common;
using Unity.Build.DotsRuntime;
using Unity.Build.iOS;

using Bee.NativeProgramSupport;
using Bee.Tools;

namespace Bee.Toolchain.IOS
{
    internal class UserIOSSdkLocator : IOSSdkLocator
    {
        public UserIOSSdkLocator() : base(Architecture.Arm64) {}

        public IOSSdk UserIOSSdk(NPath path)
        {
            return path != null ? DefaultSdkFromXcodeApp(path) : IOSSdk.LocatorArm64.FindSdkInDownloadsOrSystem(new Version(10, 1));
        }
    }

    internal class UserIOSSimulatorSdkLocator : IOSSimulatorSdkLocator
    {
        public UserIOSSimulatorSdkLocator() : base(Architecture.x64) {}

        public IOSSimulatorSdk UserIOSSdk(NPath path)
        {
            return path != null ? DefaultSdkFromXcodeApp(path) : IOSSimulatorSdk.Locatorx64.FindSdkInDownloadsOrSystem(new Version(10, 1));
        }
    }

    internal class IOSAppToolchain : IOSToolchain
    {
        private static IOSAppToolchain m_iosAppToolchain;

        public static ToolChain GetIOSAppToolchain(bool useStatic)
        {
            // wrong build configuration or non iOS toolchain
            if (!Config.Validate())
            {
                return new IOSAppToolchain(IOSSdk.LocatorArm64.UserDefaultOrDummy);
            }
            if (m_iosAppToolchain != null)
            {
                return m_iosAppToolchain;
            }
            if (useStatic)
            {
                if (Config.TargetSettings?.SdkVersion == iOSSdkVersion.DeviceSDK)
                {
                    m_iosAppToolchain = new IOSStaticLibsAppToolchain((new UserIOSSdkLocator()).UserIOSSdk(XcodePath));
                }
                else
                {
                    m_iosAppToolchain = new IOSStaticLibsAppToolchain((new UserIOSSimulatorSdkLocator()).UserIOSSdk(XcodePath));                        
                }
            }
            else
            {
                if (Config.TargetSettings?.SdkVersion == iOSSdkVersion.DeviceSDK)
                {
                    m_iosAppToolchain = new IOSAppToolchain((new UserIOSSdkLocator()).UserIOSSdk(XcodePath));
                }
                else
                {
                    m_iosAppToolchain = new IOSAppToolchain((new UserIOSSimulatorSdkLocator()).UserIOSSdk(XcodePath));                        
                }
            }
            return m_iosAppToolchain;
        }

        public override NativeProgramFormat ExecutableFormat { get; }

        // Build configuration
        internal class Config
        {
            public static GeneralSettings Settings { get; private set; }
            public static ApplicationIdentifier Identifier { get; private set; }
            public static iOSBuildNumber BuildNumber { get; private set; }
            public static iOSSigningSettings SigningSettings { get; private set; }
            public static iOSExportProject ExportProject { get; private set; }
            public static iOSTargetSettings TargetSettings { get; private set; }
            public static ScreenOrientations Orientations { get; private set; }
            public static iOSIcons Icons { get; private set; }

            public static List<string> GetAvailableOrientationList()
            {
                List<string> orientations = new List<string>();

                if (Orientations.DefaultOrientation == UIOrientation.Portrait || 
                    (Orientations.DefaultOrientation == UIOrientation.AutoRotation && Orientations.AllowAutoRotateToPortrait))
                    orientations.Add("UIInterfaceOrientationPortrait");
                if (Orientations.DefaultOrientation == UIOrientation.PortraitUpsideDown || 
                    (Orientations.DefaultOrientation == UIOrientation.AutoRotation && Orientations.AllowAutoRotateToReversePortrait))
                    orientations.Add("UIInterfaceOrientationPortraitUpsideDown");

                // we align orientation enum with apple's device orientation, so swap landscape right/left
                if (Orientations.DefaultOrientation == UIOrientation.LandscapeLeft || 
                    (Orientations.DefaultOrientation == UIOrientation.AutoRotation && Orientations.AllowAutoRotateToLandscape))
                    orientations.Add("UIInterfaceOrientationLandscapeRight");
                if (Orientations.DefaultOrientation == UIOrientation.LandscapeRight || 
                    (Orientations.DefaultOrientation == UIOrientation.AutoRotation && Orientations.AllowAutoRotateToReverseLandscape))
                    orientations.Add("UIInterfaceOrientationLandscapeLeft");

                return orientations;
            }

            public static bool Validate()
            {
                if (TargetSettings == null || Orientations == null) // not iOS toolchain
                {
                    return false;
                }
                if (Orientations.DefaultOrientation == UIOrientation.AutoRotation &&
                    !Orientations.AllowAutoRotateToPortrait &&
                    !Orientations.AllowAutoRotateToReversePortrait &&
                    !Orientations.AllowAutoRotateToLandscape &&
                    !Orientations.AllowAutoRotateToReverseLandscape)
                {
                    Console.WriteLine("There are no allowed orientations for the application");
                    return false;
                }
                return true;
            }
        }

        public static bool ExportProject => BuildConfiguration.HasComponent<iOSExportProject>();

        static IOSAppToolchain()
        {
            BuildConfiguration.Read(NPath.CurrentDirectory.Combine("buildconfiguration.json"), typeof(IOSAppToolchain.Config));
        }

        private static NPath _XcodePath = null;

        private static NPath XcodePath
        {
            get
            {
                if (_XcodePath == null)
                {
                    string error = "";

                    try
                    {
                        if (HostPlatform.IsOSX)
                        {
                            var start = new ProcessStartInfo("xcode-select", "-p")
                            {
                                RedirectStandardOutput = true,
                                RedirectStandardError = true,
                                RedirectStandardInput = true,
                                UseShellExecute = false,
                            };
                            string output = "";
                            using (var process = Process.Start(start))
                            {
                                process.OutputDataReceived += (sender, e) => { output += e.Data; };
                                process.ErrorDataReceived += (sender, e) => { error += e.Data; };
                                process.BeginOutputReadLine();
                                process.BeginErrorReadLine();
                                process.WaitForExit(); //? doesn't work correctly if time interval is set
                            }

                            _XcodePath = error == "" ? output : "";
                            if (_XcodePath != "" && _XcodePath.DirectoryExists())
                            {
                                _XcodePath = XcodePath.Parent.Parent;
                            }
                            else
                            {
                                throw new InvalidOperationException("Failed to find Xcode, xcode-select did not return a valid path");
                            }
                        }
                    }
                    catch (InvalidOperationException e)
                    {
                        Console.WriteLine(
                            $"xcode-select did not return a valid path. Error message, if any, was: {error}. " +
                            $"Often this can be fixed by making sure you have Xcode command line tools" +
                            $" installed correctly, and then running `sudo xcode-select -r`");
                        throw e;
                    }
                }

                return _XcodePath;
            }
        }

        public static NPath XcodeBuildPath
        {
            get { return XcodePath != null ? XcodePath.Combine("Contents", "Developer", "usr", "bin", "xcodebuild") : null; }
        }

        public IOSAppToolchain(IOSSdk sdk) : base(sdk)
        {
            ExecutableFormat = new IOSAppMainModuleFormat(this);
        }

        public IOSAppToolchain(IOSSimulatorSdk sdk) : base(sdk)
        {
            ExecutableFormat = new IOSAppMainModuleFormat(this);
        }
    }

    internal class IOSStaticLibsAppToolchain : IOSAppToolchain
    {
        public override NativeProgramFormat DynamicLibraryFormat { get; }

        public IOSStaticLibsAppToolchain(IOSSdk sdk) : base(sdk)
        {
            DynamicLibraryFormat = StaticLibraryFormat;
        }        
        public IOSStaticLibsAppToolchain(IOSSimulatorSdk sdk) : base(sdk)
        {
            DynamicLibraryFormat = StaticLibraryFormat;
        }        
    }

    internal sealed class IOSAppMainModuleFormat : NativeProgramFormat
    {
        public override string Extension { get; } = IOSAppToolchain.ExportProject ? "" : "app";

        internal IOSAppMainModuleFormat(XcodeToolchain toolchain) : base(
            new IOSAppMainModuleLinker(toolchain).WithBundledStaticLibraryDependencies(true))
        {
        }
    }

    internal class IOSAppMainModuleLinker : XcodeStaticLinker
    {
        private NPath ChangeMainModuleName(NPath target)
        {
            return target.ChangeExtension("a");
        }

        public IOSAppMainModuleLinker(ToolChain toolChain) : base(toolChain) {}

        protected override IEnumerable<string> CommandLineFlagsFor(NPath destination, CodeGen codegen, IEnumerable<NPath> objectFiles)
        {
            foreach (var flag in base.CommandLineFlagsFor(ChangeMainModuleName(destination), codegen, objectFiles))
            {
                yield return flag;
            }
        }

        protected override BuiltNativeProgram BuiltNativeProgramFor(NPath destination, IEnumerable<PrecompiledLibrary> allLibraries)
        {
            var resultingLibs = BundleStaticLibraryDependencies ? allLibraries.Where(a => !a.Static) : allLibraries;

            return (BuiltNativeProgram)new IOSAppMainStaticLibrary(ChangeMainModuleName(destination), Toolchain as IOSAppToolchain, resultingLibs.ToArray());
        }
    }

    internal class IOSAppMainStaticLibrary : StaticLibrary, IPackagedAppExtension
    {
        private const string TinyProjectName = "Tiny-iPhone";

        private IOSAppToolchain m_iosAppToolchain;
        private String m_gameName;
        private DotsConfiguration m_config;
        private IEnumerable<IDeployable> m_supportFiles;

        public IOSAppMainStaticLibrary(NPath path, IOSAppToolchain toolchain, params PrecompiledLibrary[] libraryDependencies) : base(path, libraryDependencies)
        {
            m_iosAppToolchain = toolchain;
        }

        public void SetAppPackagingParameters(String gameName, DotsConfiguration config, IEnumerable<IDeployable> supportFiles)
        {
            m_gameName = gameName;
            m_config = config;
            m_supportFiles = supportFiles;
        }

        public override BuiltNativeProgram DeployTo(NPath targetDirectory, Dictionary<IDeployable, IDeployable> alreadyDeployed = null)
        {
            var libDirectory = IOSAppToolchain.ExportProject ? targetDirectory.Combine(m_gameName) : Path.Parent.Combine(TinyProjectName);
            var result = base.DeployTo(libDirectory, alreadyDeployed);
            return new Executable(PackageApp(targetDirectory, result.Path));
        }

        private NPath PackageApp(NPath buildPath, NPath mainLibPath)
        {
            if (m_iosAppToolchain == null)
            {
                Console.WriteLine("Error: not IOS App toolchain");
                return mainLibPath;
            }

            var pbxPath = GenerateXCodeProject(mainLibPath);

            var deployedPath = buildPath.Combine($"{m_gameName}{(IOSAppToolchain.ExportProject ? "" : ".app")}");
            if (IOSAppToolchain.ExportProject)
            {
                Backend.Current.AddAction(
                    actionName: "Open Xcode project folder",
                    targetFiles: new[] { deployedPath },
                    inputs: new[] { pbxPath },
                    executableStringFor: $"open {deployedPath}",
                    commandLineArguments: Array.Empty<string>(),
                    allowUnexpectedOutput: true,
                    allowUnwrittenOutputFiles: true
                );
            }
            else
            {
                var configuration = m_config == DotsConfiguration.Release ? "Release" : "Debug";
                var xcodeprojPath = pbxPath.Parent;
                var outputPath = xcodeprojPath.Parent.Combine("app");
                var target = IOSAppToolchain.Config.TargetSettings.SdkVersion == iOSSdkVersion.DeviceSDK ? "iphoneos" : "iphonesimulator";
                var appPath = outputPath.Combine("Build", "Products", $"{configuration}-{target}", $"{TinyProjectName}.app");
                var destination = IOSAppToolchain.Config.TargetSettings.SdkVersion == iOSSdkVersion.DeviceSDK ? "generic/platform=iOS": "platform=iOS Simulator,name=iPhone 11";
                var xcodeBuildExecutableString = $"{IOSAppToolchain.XcodeBuildPath.InQuotes()} -project {xcodeprojPath.InQuotes()} -configuration {configuration} -derivedDataPath {outputPath.InQuotes()} -destination \"{destination}\" -scheme \"{TinyProjectName}\" -allowProvisioningUpdates";
                Backend.Current.AddAction(
                    actionName: "Build Xcode project",
                    targetFiles: new[] { appPath },
                    inputs: new[] { pbxPath },
                    executableStringFor: xcodeBuildExecutableString,
                    commandLineArguments: Array.Empty<string>(),
                    allowUnexpectedOutput: true,
                    allowUnwrittenOutputFiles: true
                );

                Backend.Current.AddAction(
                    actionName: "Copy application to output folder",
                    targetFiles: new[] { deployedPath },
                    inputs: new[] { appPath },
                    executableStringFor: $"rm -rf {deployedPath} && cp -R {appPath} {deployedPath}",
                    commandLineArguments: Array.Empty<string>(),
                    allowUnexpectedOutput: true
                );
            }
            return deployedPath;
        }

        private NPath GenerateXCodeProject(NPath mainLibPath)
        {
            var outputPath = mainLibPath.Parent;
            var iosPlatformPath = AsmDefConfigFile.AsmDefDescriptionFor("Unity.Build.iOS.DotsRuntime").Path.Parent;
            var xcodeSrcPath = iosPlatformPath.Combine(TinyProjectName+"~");
            var xcodeprojPath = outputPath.Combine($"{TinyProjectName}.xcodeproj");

            // copy and patch pbxproj file
            var pbxPath = xcodeprojPath.Combine("project.pbxproj");
            var pbxTemplatePath = xcodeSrcPath.Combine($"{TinyProjectName}.xcodeproj", "project.pbxproj");
            var result = SetupXCodeProject(pbxTemplatePath);
            Backend.Current.AddWriteTextAction(pbxPath, result);
            Backend.Current.AddDependency(pbxPath, mainLibPath);

            // copy and patch xcscheme file
            var xcschemePath = xcodeprojPath.Combine("xcshareddata", "xcschemes", "Tiny-iPhone.xcscheme");
            var xcschemeTemplatePath = xcodeSrcPath.Combine($"{TinyProjectName}.xcodeproj", "xcshareddata", "xcschemes", "Tiny-iPhone.xcscheme");
            result = SetupXcScheme(xcschemeTemplatePath, m_config == DotsConfiguration.Release);
            Backend.Current.AddWriteTextAction(xcschemePath, result);
            Backend.Current.AddDependency(pbxPath, xcschemePath);

            // copy and patch Info.plist file
            var plistPath = outputPath.Combine("Sources", "Info.plist");
            var plistTemplatePath = xcodeSrcPath.Combine("Sources", "Info.plist");
            result = SetupInfoPlist(plistTemplatePath);
            Backend.Current.AddWriteTextAction(plistPath, result);
            Backend.Current.AddDependency(xcschemePath, plistPath);

            // copy xcodeproj files
            foreach (var r in xcodeSrcPath.Files(true))
            {
                if (r.Extension != "pbxproj" && r.Extension != "xcscheme" && r.FileName != "Info.plist" && !r.HasDirectory("AppIcon.appiconset"))
                {
                    var destPath = outputPath.Combine(r.RelativeTo(xcodeSrcPath));
                    destPath = CopyTool.Instance().Setup(destPath, r);
                    Backend.Current.AddDependency(pbxPath, destPath);
                }
            }

            // copy icon files
            var icons = IOSAppToolchain.Config.Icons;
            Backend.Current.AddDependency(pbxPath, CopyIcon(xcodeSrcPath, outputPath, "Icon-iPhone-120.png", icons.iPhone2x));
            Backend.Current.AddDependency(pbxPath, CopyIcon(xcodeSrcPath, outputPath, "Icon-iPhone-180.png", icons.iPhone3x));
            Backend.Current.AddDependency(pbxPath, CopyIcon(xcodeSrcPath, outputPath, "Icon-iPad-152.png", icons.iPad2x));
            Backend.Current.AddDependency(pbxPath, CopyIcon(xcodeSrcPath, outputPath, "Icon-iPad-167.png", icons.iPadPro2x));
            Backend.Current.AddDependency(pbxPath, CopyIcon(xcodeSrcPath, outputPath, "AppStore-1024.png", icons.AppStore));
            Backend.Current.AddDependency(pbxPath, CopyIcon(xcodeSrcPath, outputPath, "Contents.json", null));

            foreach (var r in m_supportFiles)
            {
                if (r.Path.FileName == "testconfig.json")
                {
                    Backend.Current.AddDependency(pbxPath, CopyTool.Instance().Setup(outputPath.Combine(r.Path.FileName), r.Path)); 
                    break;
                }
            }
            return pbxPath;
        }

        private NPath CopyIcon(NPath srcPath, NPath destPath, string iconName, string configIcon)
        {
            const string iconsPath = "Sources/Assets.xcassets/AppIcon.appiconset";
            destPath = destPath.Combine(iconsPath, iconName);
            srcPath = !String.IsNullOrEmpty(configIcon) ? configIcon : srcPath.Combine(iconsPath, iconName);
            return CopyTool.Instance().Setup(destPath, srcPath);
        }

        private void ProcessLibs(BuiltNativeProgram p, HashSet<NPath> xCodeLibs)
        {
            if (p.Path.Extension == "dylib" || p.Path.Extension == "a")
            {
                xCodeLibs.Add(p.Path);
            }
            foreach (var d in p.Deployables)
            {
                if (d is BuiltNativeProgram)
                {
                    ProcessLibs(d as BuiltNativeProgram, xCodeLibs);
                }
            }
        }

        private string SetupXCodeProject(NPath pbxTemplatePath)
        {
            PBXProject pbxProject = new PBXProject();
            pbxProject.ReadFromFile(pbxTemplatePath.ToString());
            var target = pbxProject.TargetGuidByName(TinyProjectName);
            var targets = new string[] { target };

            // preparing list of libs and adding them to project
            HashSet<NPath> xCodeLibs = new HashSet<NPath>();
            ProcessLibs(this, xCodeLibs);
            foreach (var lib in xCodeLibs)
            {
                var fileGuid = pbxProject.AddFile(lib.FileName, lib.FileName);
                pbxProject.AddFileToBuild(target, fileGuid);
                if (lib.Extension == "dylib")
                {
                    PBXProjectExtensions.AddFileToEmbedFrameworks(pbxProject, target, fileGuid);
                }
            }

            var dataExists = false;
            foreach (var r in m_supportFiles)
            {
                // skipping all subdirectories
                // TODO: subdirectories require special processing (see processing Data below)
                var depth = (r as DeployableFile)?.RelativeDeployPath?.Depth;
                if ((!depth.HasValue || depth <= 1) && r.Path.FileName != "testconfig.json") // fix this condition somehow
                {
                    var fileGuid = pbxProject.AddFile(r.Path.FileName, r.Path.FileName);
                    pbxProject.AddFileToBuild(target, fileGuid);
                }
                else if (r.Path.HasDirectory("Data"))
                {
                    dataExists = true;
                }
            }
            // adding Data folder
            if (dataExists)
            {
                var fileGuid = pbxProject.AddFile("Data", "Data");
                pbxProject.AddFileToBuild(target, fileGuid);
            }

            pbxProject.SetBuildProperty(targets, "PRODUCT_BUNDLE_IDENTIFIER", IOSAppToolchain.Config.Identifier.PackageName);
            pbxProject.SetBuildProperty(targets, "IPHONEOS_DEPLOYMENT_TARGET",IOSAppToolchain.Config.TargetSettings.TargetVersion.ToString(2));
            pbxProject.SetBuildProperty(targets, "CODE_SIGN_STYLE", "Automatic");
            pbxProject.SetBuildProperty(targets, "PROVISIONING_PROFILE", "");
            pbxProject.SetBuildProperty(targets, "ARCHS", IOSAppToolchain.Config.TargetSettings.GetTargetArchitecture());

            pbxProject.SetBuildProperty(targets, "SDKROOT", IOSAppToolchain.Config.TargetSettings.SdkVersion == iOSSdkVersion.DeviceSDK ? "iphoneos" : "iphonesimulator");
            pbxProject.RemoveBuildProperty(targets, "SUPPORTED_PLATFORMS");
            pbxProject.AddBuildProperty(targets, "SUPPORTED_PLATFORMS", "iphoneos");
            if (IOSAppToolchain.Config.TargetSettings.SdkVersion == iOSSdkVersion.SimulatorSDK)
                pbxProject.AddBuildProperty(targets, "SUPPORTED_PLATFORMS", "iphonesimulator");
            pbxProject.SetBuildProperty(targets, "TARGETED_DEVICE_FAMILY", IOSAppToolchain.Config.TargetSettings.GetTargetDeviceFamily());

            pbxProject.SetBuildProperty(targets, "DEVELOPMENT_TEAM", IOSAppToolchain.Config.SigningSettings.SigningTeamID);
            if (!IOSAppToolchain.Config.SigningSettings.AutomaticallySign)
            {
                pbxProject.SetBuildProperty(targets, "PROVISIONING_PROFILE", IOSAppToolchain.Config.SigningSettings.ProfileID);
                pbxProject.SetBuildProperty(targets, "CODE_SIGN_IDENTITY[sdk=iphoneos*]", IOSAppToolchain.Config.SigningSettings.CodeSignIdentityValue);
                pbxProject.SetBuildProperty(targets, "CODE_SIGN_STYLE", "Manual");
            }
            else
            {
                pbxProject.SetBuildProperty(targets, "CODE_SIGN_STYLE", "Automatic");
                // set manual profiles to nothing if automatically signing
                pbxProject.SetBuildProperty(targets, "PROVISIONING_PROFILE", "");
            }
            return pbxProject.WriteToString().Replace("**ORGANIZATION**", IOSAppToolchain.Config.Settings.CompanyName);
        }

        private string SetupXcScheme(NPath xcSchemePath, bool release)
        {
            var text = xcSchemePath.ReadAllText();
            var xcscheme= new XcScheme();
            xcscheme.ReadFromString(text);
            string buildConfigName = release ? "ReleaseForRunning" : "Debug";
            xcscheme.SetBuildConfiguration(buildConfigName);
            return xcscheme.WriteToString();
        }

        private string SetupInfoPlist(NPath plistTemplatePath)
        {
            var text = plistTemplatePath.ReadAllText();
            var doc = new PlistDocument();
            doc.ReadFromString(text);
            var root = doc.root;
            root.SetString("CFBundleIdentifier", IOSAppToolchain.Config.Identifier.PackageName);
            root.SetString("CFBundleDisplayName", IOSAppToolchain.Config.Settings.ProductName);
            var version = IOSAppToolchain.Config.Settings.Version;
            var fieldCount = version.Revision > 0 ? 4 : 3;
            root.SetString("CFBundleShortVersionString", IOSAppToolchain.Config.Settings.Version.ToString(fieldCount));
            var buildNumber = IOSAppToolchain.Config.BuildNumber.BuildNumber;
            fieldCount = buildNumber.Build > 0 ? 3 : (buildNumber.Minor > 0 ? 2 : 1);
            root.SetString("CFBundleVersion", buildNumber.ToString(fieldCount));

            var orient = root.CreateArray("UISupportedInterfaceOrientations");
            var orient_ipad = root.CreateArray("UISupportedInterfaceOrientations~ipad");
            foreach (var s in IOSAppToolchain.Config.GetAvailableOrientationList())
            {
                orient.AddString(s);
                orient_ipad.AddString(s);
            }

            return doc.WriteToString();
        }
    }
}

