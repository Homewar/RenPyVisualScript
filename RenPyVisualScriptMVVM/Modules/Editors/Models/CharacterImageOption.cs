namespace RenPyVisualScriptMVVM.Modules.Editors.Models;

public sealed class CharacterImageOption
{
    public CharacterImageOption(string label, string? value, string? sourcePath = null)
    {
        Label = label;
        Value = value;
        SourcePath = sourcePath;
    }

    public string Label { get; }
    public string? Value { get; }
    public string? SourcePath { get; }

    public override string ToString() => Label;
}
