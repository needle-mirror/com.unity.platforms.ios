using System;
using System.IO;
using System.Text;
using System.Diagnostics;

namespace Unity.Platforms.IOS
{
    public class IOSBuildTarget : BuildTarget
    {
        public override bool HideInBuildTargetPopup => UnityEngine.Application.platform != UnityEngine.RuntimePlatform.OSXEditor;

        public override string GetDisplayName()
        {
            return "IOS";
        }

        public override string GetBeeTargetName()
        {
            return "ios";
        }

        public override string GetExecutableExtension()
        {
            return ".command";
        }

        public override string GetUnityPlatformName()
        {
            return nameof(UnityEditor.BuildTarget.iOS);
        }

        public override bool Run(FileInfo buildTarget)
        {
            var buildDir = buildTarget.Directory.FullName;
            var result = Shell.RunAsync(new ShellProcessArgs()
            {
                ThrowOnError = false,
                Executable = "sh", 
                Arguments = new string[] { buildTarget.FullName },
                WorkingDirectory = new DirectoryInfo(buildDir),
                OutputDataReceived = (object sender, DataReceivedEventArgs args) => { if (args.Data != null) UnityEngine.Debug.Log(args.Data); },
                ErrorDataReceived = (object sender, DataReceivedEventArgs args) => {  if (args.Data != null) UnityEngine.Debug.LogError(args.Data); }
            });
            return true;
        }

        public override ShellProcessOutput RunTestMode(string exeName, string workingDirPath, int timeout)
        {
            var args = new string[] { $"{workingDirPath}/{exeName}{GetExecutableExtension()}" };
            var workingDir = new DirectoryInfo(workingDirPath);

            System.Threading.EventWaitHandle started =  new System.Threading.AutoResetEvent(false);
            var logOutput = new StringBuilder();
            DataReceivedEventHandler outputReceived = (object sender, DataReceivedEventArgs e) =>
            {
                if (e.Data != null)
                {
                    logOutput.AppendLine(e.Data);
                    if (e.Data.Contains("(lldb)") && e.Data.Contains("run"))
                    {
                        started.Set();
                    }
                }
            };

            DataReceivedEventHandler errorReceived = (object sender, DataReceivedEventArgs e) =>
            {
                if (e.Data != null)
                {
                    logOutput.AppendLine(e.Data);
                }
            };

            var shellArgs = new ShellProcessArgs
            {
                Executable = "sh", 
                Arguments = args,
                WorkingDirectory = workingDir,
                ThrowOnError = false,
                OutputDataReceived = outputReceived,
                ErrorDataReceived = errorReceived
            };


            Shell.RunAsync(shellArgs);

            // waiting for process to start on the device
            started.WaitOne();

            // Killing on timeout
            // TODO auto exit for non-samples tests
            System.Threading.Thread.Sleep((timeout == 0 ? 2000 : timeout) + 3000); // starting on iOS is slow

            // killing ios-deploy to kill running app on device
            Shell.Run(new ShellProcessArgs()
            {
                Executable = "pkill", 
                Arguments = new string[] { "ios-deploy" },
                WorkingDirectory = workingDir,
                ThrowOnError = false
            });

            var fullOutput = logOutput.ToString();
            return new ShellProcessOutput
            {
                // timeout == 0 is for non-sample test, TODO invent something better
                Succeeded = timeout == 0 ? fullOutput.Contains("Test suite: SUCCESS") : true,
                ExitCode = 0,
                FullOutput = fullOutput
            };
        }
    }
}
