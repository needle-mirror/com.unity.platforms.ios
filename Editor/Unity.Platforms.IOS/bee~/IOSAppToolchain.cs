using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using Bee.NativeProgramSupport.Building;
using Bee.Core;
using Bee.DotNet;
using Bee.Stevedore;
using Bee.Toolchain.Xcode;
using Bee.Toolchain.Extension;
using Bee.BuildTools;
using Newtonsoft.Json.Linq;
using NiceIO;
using Unity.BuildSystem.NativeProgramSupport;
using Unity.BuildTools;
using UnityEditor.iOS.Xcode.Custom;
using UnityEditor.iOS.Xcode.Custom.Extensions;

namespace Bee.Toolchain.IOS
{
    internal class IOSAppToolchain : IOSToolchain
    {
        public static ToolChain ToolChain_IOSAppArm64 { get; } = new IOSAppToolchain(IOSSdk.LocatorArm64.FindSdkInDownloadsOrSystem(new Version(10, 1)));

        public override NativeProgramFormat ExecutableFormat { get; }

        public NPath XcodeBuildPath { get; private set; }

        public IOSAppToolchain(IOSSdk sdk) : base(sdk)
        {
            ExecutableFormat = new IOSAppMainModuleFormat(this);

            // getting path to xcodebuild
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
                string error = "";
                using (var process = Process.Start(start))
                {
                    process.OutputDataReceived += (sender, e) => { output += e.Data; };
                    process.ErrorDataReceived += (sender, e) => { error += e.Data; };
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                    process.WaitForExit(); //? doesn't work correctly if time interval is set
                }
                XcodeBuildPath = error == "" ? output : "";
                if (XcodeBuildPath != "" && XcodeBuildPath.DirectoryExists())
                {
                    XcodeBuildPath = XcodeBuildPath.Combine("usr", "bin", "xcodebuild");
                }
                else
                {
                    throw new Exception("Failed to find Xcode, xcode-select did not return a valid path");
                }
            }
            else
            {
                XcodeBuildPath = "";
            }
        }
    }

    internal sealed class IOSAppMainModuleFormat : NativeProgramFormat
    {
        public override string Extension { get; } = "command";

        internal IOSAppMainModuleFormat(XcodeToolchain toolchain) : base(
            new IOSAppMainModuleLinker(toolchain))
        {
        }
    }

    //TODO: should be inherited from XcodeStaticLinker, but it is sealed
    internal class IOSAppMainModuleLinker : StaticLinker
    {
        private NPath ChangeMainModuleName(NPath target)
        {
            // need to rename to make it start with "lib", otherwise Android have problems with loading native library
            return target.ChangeExtension("a");
        }

        protected override bool SupportsResponseFile => false; // libtool does not support response files

        public IOSAppMainModuleLinker(ToolChain toolChain) : base(toolChain) {}

        protected override IEnumerable<string> CommandLineFlagsForLibrary(PrecompiledLibrary library, CodeGen codegen)
        {
            if (BundleStaticLibraryDependencies && library.Static)
                yield return library.InQuotes();
        }

        protected override IEnumerable<string> CommandLineFlagsFor(NPath destination, CodeGen codegen, IEnumerable<NPath> objectFiles)
        {
            if (Toolchain.Architecture is ARMv7Architecture)
            {
                yield return "-arch_only";
                yield return "armv7";
            }

            if (Toolchain.Architecture is Arm64Architecture)
            {
                yield return "-arch_only";
                yield return "arm64";
            }

            yield return "-static";

            foreach (var objectFile in objectFiles)
                yield return objectFile.InQuotes();

            yield return "-o";
            yield return ChangeMainModuleName(destination).InQuotes();
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
        private CodeGen m_codeGen;
        private IEnumerable<IDeployable> m_supportFiles;

        public IOSAppMainStaticLibrary(NPath path, IOSAppToolchain toolchain, params PrecompiledLibrary[] libraryDependencies) : base(path, libraryDependencies) 
        {
            m_iosAppToolchain = toolchain;
        }

        public void SetAppPackagingParameters(String gameName, CodeGen codeGen, IEnumerable<IDeployable> supportFiles)
        {
            m_gameName = gameName.Replace(".","-");
            m_codeGen = codeGen;
            m_supportFiles = supportFiles;
        }

        public override BuiltNativeProgram DeployTo(NPath targetDirectory, Dictionary<IDeployable, IDeployable> alreadyDeployed = null)
        {
            var libDirectory = Path.Parent.Combine(TinyProjectName);
            var result = base.DeployTo(libDirectory, alreadyDeployed);
            return new Executable(PackageApp(targetDirectory, result.Path));
        }

        public NPath PackageApp(NPath buildPath, NPath mainLibPath)
        {
            if (m_iosAppToolchain == null)
            {
                Console.WriteLine("Error: not IOS App toolchain");
                return mainLibPath;
            }

            var iosPlatformPath = AsmDefConfigFile.AsmDefDescriptionFor("Unity.Platforms.IOS").Path.Parent;
            var xcodeProjectPath = mainLibPath.Parent;
            var xcodeSrcPath = iosPlatformPath.Combine(TinyProjectName+"~");
            var xcodeprojPath = xcodeProjectPath.Combine($"{TinyProjectName}.xcodeproj");
            var appFolderPath = buildPath.Combine("app");
            var configuration = m_codeGen == CodeGen.Release ? "Release" : "Debug";
            var appPath = appFolderPath.Combine("Build", "Products", $"{configuration}-iphoneos", $"{TinyProjectName}.app");

            // copy and patch pbxproj file
            var pbxPath = xcodeprojPath.Combine("project.pbxproj");
            var pbxTemplatePath = xcodeSrcPath.Combine($"{TinyProjectName}.xcodeproj", "project.pbxproj");
            var exportManifestPath = new NPath(m_gameName).Combine("export.manifest");
            var result = SetupXCodeProject(pbxTemplatePath, exportManifestPath.FileExists());
            Backend.Current.AddWriteTextAction(pbxPath, result);
            Backend.Current.AddDependency(pbxPath, mainLibPath);

            // copy and patch Info.plist file
            var plistPath = xcodeProjectPath.Combine("Sources", "Info.plist");
            var plistTemplatePath = xcodeSrcPath.Combine("Sources", "Info.plist");
            result = SetupInfoPlist(plistTemplatePath);
            Backend.Current.AddWriteTextAction(plistPath, result);
            Backend.Current.AddDependency(pbxPath, plistPath);

            // copy xcodeproj files
            foreach (var r in xcodeSrcPath.Files(true))
            {
                if (r.Extension != "pbxproj" && r.FileName != "Info.plist")
                {
                    var destPath = xcodeProjectPath.Combine(r.RelativeTo(xcodeSrcPath));
                    destPath = CopyTool.Instance().Setup(destPath, r);
                    Backend.Current.AddDependency(pbxPath, destPath);
                }
            }

            var xcodeBuildExecutableString = $"{m_iosAppToolchain.XcodeBuildPath.InQuotes()} -project {xcodeprojPath.InQuotes()} -configuration {configuration} -derivedDataPath {appFolderPath.InQuotes()} -destination \"generic/platform=iOS\" -scheme \"{TinyProjectName}\" -allowProvisioningUpdates";
            Backend.Current.AddAction(
                actionName: "Build Xcode project",
                targetFiles: new[] { appPath },
                inputs: new[] { pbxPath },
                executableStringFor: xcodeBuildExecutableString,
                commandLineArguments: Array.Empty<string>(),
                allowUnexpectedOutput: true
            );

            // write command file to launch
            var cmdPath = buildPath.Combine(m_gameName).ChangeExtension("command");
            var iosDeployPath = iosPlatformPath.Combine("ios-deploy~/ios-deploy");
            var runCmd = "#!/bin/bash\n" + $"{iosDeployPath} -b {appPath.MakeAbsolute()} -r -d -I -W";
            Backend.Current.AddWriteTextAction(cmdPath, runCmd);
            Backend.Current.AddDependency(cmdPath, appPath);

            foreach (var r in m_supportFiles)
            {
                if (r.Path.FileName == "testconfig.json")
                {
                    Backend.Current.AddDependency(cmdPath, CopyTool.Instance().Setup(buildPath.Combine(r.Path.FileName), r.Path));
                    break;
                }
            }

           return cmdPath;
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

        private string SetupXCodeProject(NPath pbxTemplatePath, bool dataExists)
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

            foreach (var r in m_supportFiles)
            {
                // skipping all subdirectories
                // TODO: subdirectories require special processing (see processing Data below)
                if (r.Path.RelativeTo(Path).Depth == 0 && r.Path.FileName != "testconfig.json")
                {
                    var fileGuid = pbxProject.AddFile(r.Path.FileName, r.Path.FileName);
                    pbxProject.AddFileToBuild(target, fileGuid);
                }
            }
            // adding Data folder
            if (dataExists)
            {
                var fileGuid = pbxProject.AddFile("Data", "Data");
                pbxProject.AddFileToBuild(target, fileGuid);
            }

            string appleDeveloperTeamID = null;
            string manualProvisioningProfileName = null;
            string manualProvisioningProfileUUID = null;
            string codeSignIdentity = null;

            //TODO pass signing config from build settings

            appleDeveloperTeamID = Environment.GetEnvironmentVariable("TEAM_ID");
            manualProvisioningProfileName = Environment.GetEnvironmentVariable("UNITY_IOSPROVISIONINGNAME");
            manualProvisioningProfileUUID = Environment.GetEnvironmentVariable("UNITY_IOSPROVISIONINGUUID");
            codeSignIdentity = Environment.GetEnvironmentVariable("UNITY_APPLECERTIFICATENAME");

            pbxProject.SetBuildProperty(targets, "PRODUCT_BUNDLE_IDENTIFIER", $"com.unity.{m_gameName.ToLower()}");
            if (!string.IsNullOrEmpty(appleDeveloperTeamID))
            {
                pbxProject.SetBuildProperty(targets, "DEVELOPMENT_TEAM", appleDeveloperTeamID);
            }
            if (string.IsNullOrEmpty(manualProvisioningProfileUUID) && string.IsNullOrEmpty(manualProvisioningProfileName))
            {
                pbxProject.SetBuildProperty(targets, "CODE_SIGN_STYLE", "Automatic");
                pbxProject.SetBuildProperty(targets, "PROVISIONING_PROFILE", "Automatic");
            }
            else
            {
                pbxProject.SetBuildProperty(targets, "CODE_SIGN_STYLE", "Manual");
                pbxProject.SetBuildProperty(targets, "PROVISIONING_PROFILE", !string.IsNullOrEmpty(manualProvisioningProfileUUID) ? manualProvisioningProfileUUID : manualProvisioningProfileName);
                pbxProject.SetBuildProperty(targets, "CODE_SIGN_IDENTITY[sdk=iphoneos*]", codeSignIdentity == null ? "iPhone Developer" : codeSignIdentity);
            }
            return pbxProject.WriteToString();
        }

        private string SetupInfoPlist(NPath plistTemplatePath)
        {
            var text = plistTemplatePath.ReadAllText();
            var doc = new PlistDocument();
            doc.ReadFromString(text);
            var root = doc.root;
            root.SetString("CFBundleIdentifier", $"com.unity.{m_gameName.ToLower()}");
            root.SetString("CFBundleDisplayName", m_gameName);
            return doc.WriteToString();
        }
    }
}

