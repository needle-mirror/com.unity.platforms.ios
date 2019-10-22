using Bee.NativeProgramSupport.Building;
using Bee.Toolchain.IOS;
using DotsBuildTargets;
using Unity.BuildSystem.NativeProgramSupport;

class DotsIOSTarget : DotsBuildSystemTarget
{
    protected override NativeProgramFormat GetExecutableFormatForConfig(DotsConfiguration config, bool enableManagedDebugger)
    {
        return new IOSAppMainModuleFormat(ToolChain as IOSAppToolchain);
    }

    protected override string Identifier => "ios";

    protected override ToolChain ToolChain => IOSAppToolchain.ToolChain_IOSAppArm64;
}
