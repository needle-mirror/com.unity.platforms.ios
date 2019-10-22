using Bee.Toolchain.Xcode;
using JetBrains.Annotations;
using Unity.BuildSystem.NativeProgramSupport;

[UsedImplicitly]
class CustomizerForTinyIOS : AsmDefCSharpProgramCustomizer
{
    public override string CustomizerFor => "Unity.Tiny.IOS";

    // not exactly right, but good enough for now
    public override string[] ImplementationFor => new[] {"Unity.Tiny.Core"};

    public override void CustomizeSelf(AsmDefCSharpProgram program)
    {
        program.NativeProgram.Libraries.Add(new SystemFramework("OpenGLES"));
    }
}
