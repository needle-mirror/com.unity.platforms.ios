using System.IO;
using Unity.Build;
using UnityEditor.Build.Reporting;
using BuildResult = Unity.Build.BuildResult;

namespace Unity.Platforms.iOS.Build
{
    [BuildStep(Description = "Producing iOS Artifacts")]
    sealed class iOSProduceArtifactStep : BuildStepBase
    {
        public override BuildResult Run(BuildContext context)
        {
            var report = context.GetValue<BuildReport>();
            if (report == null)
            {
                return context.Failure($"Could not retrieve {nameof(BuildReport)} from build context.");
            }

            var artifact = context.GetOrCreateValue<iOSBuildArtifact>();
            artifact.OutputTargetFile = new FileInfo(report.summary.outputPath);
            return context.Success();
        }
    }
}
