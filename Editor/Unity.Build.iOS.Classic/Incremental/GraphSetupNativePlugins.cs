#if ENABLE_EXPERIMENTAL_INCREMENTAL_PIPELINE
using Bee.Core;
using NiceIO;
using System.IO;
using System.Linq;
using Unity.Build;
using Unity.Build.Classic.Private.IncrementalClassicPipeline;
using UnityEditor;

namespace Unity.Build.iOS.Classic
{
    sealed class GraphSetupNativePlugins : BuildStepBase
    {
        public override BuildResult Run(BuildContext context)
        {
            var classicContext = context.GetValue<IncrementalClassicSharedData>();
            var buildTarget = classicContext.BuildTarget;

            var nativePlugins = PluginImporter.GetImporters(buildTarget).Where(m => m.isNativePlugin);
            foreach (var p in nativePlugins)
            {
                CopyTool.Instance().Setup(classicContext.Architectures[Architecture.Arm64].DynamicLibraryDeployDirectory.Combine(Path.GetFileName(p.assetPath)), new NPath(p.assetPath).MakeAbsolute());
            }
            return context.Success();
        }
    }
}
#endif
