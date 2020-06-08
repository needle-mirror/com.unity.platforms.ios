#if ENABLE_EXPERIMENTAL_INCREMENTAL_PIPELINE
using Bee.Core;
using NiceIO;
using System;
using Unity.Build;
using Unity.Build.Classic.Private.IncrementalClassicPipeline;
using Unity.Build.Common;

namespace Unity.Build.iOS.Classic
{
    sealed class GraphSetupPlayerFiles : BuildStepBase
    {
        public override Type[] UsedComponents { get; } = { typeof(GeneralSettings) };
        public override BuildResult Run(BuildContext context)
        {
            var classicContext = context.GetValue<IncrementalClassicSharedData>();
            var playerDirectory = classicContext.VariationDirectory;

            var appName = new NPath(context.GetComponentOrDefault<GeneralSettings>().ProductName + ".app");

            NPath outputBuildDirectory = new NPath(context.GetOutputBuildDirectory());
            foreach (var file in playerDirectory.Files(true))
            {
                if (file.Parent.FileName == "Managed")
                    continue;

                if (file.FileName == ".DS_Store")
                    continue;

                if (file.Extension == "dll" || file.Extension == "pdb")
                    continue;

                var targetRelativePath = file.RelativeTo(playerDirectory);
                if (targetRelativePath.ToString().StartsWith("Data/"))
                    targetRelativePath = appName.Combine("Contents", "Resources", "Data");

                CopyTool.Instance().Setup(outputBuildDirectory.Combine(targetRelativePath), file.MakeAbsolute());
            }

            return context.Success();
        }
    }
}
#endif
