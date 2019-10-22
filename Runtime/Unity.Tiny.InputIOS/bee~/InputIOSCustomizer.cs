using JetBrains.Annotations;

[UsedImplicitly]
class CustomizerForInputIOS : AsmDefCSharpProgramCustomizer
{
    public override string CustomizerFor => "Unity.Tiny.InputIOS";

    public override string[] ImplementationFor => new [] { "Unity.Tiny.Input" };
}
