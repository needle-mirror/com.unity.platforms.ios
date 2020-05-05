#if ENABLE_EXPERIMENTAL_INCREMENTAL_PIPELINE
using Bee.Core;
using Unity.Build;
using Unity.Build.Classic.Private.IncrementalClassicPipeline;

namespace Unity.Platforms.iOS.Build
{
    sealed class GraphCopyDefaultResources : BuildStepBase
    {
        public override BuildResult Run(BuildContext context)
        {
            var classicContext = context.GetValue<IncrementalClassicSharedData>();
            CopyTool.Instance().Setup(
                classicContext.DataDeployDirectory.Combine("unity default resources"),
                classicContext.PlayerPackageDirectory.Combine("Trampoline", "Data", "unity default resources"));

            return context.Success();
        }
    }
}
#endif
