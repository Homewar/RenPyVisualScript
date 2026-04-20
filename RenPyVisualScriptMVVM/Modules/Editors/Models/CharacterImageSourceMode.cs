namespace RenPyVisualScriptMVVM.Modules.Editors.Models;

public sealed class CharacterImageSourceMode
{
    public CharacterImageSourceMode(string title, string key)
    {
        Title = title;
        Key = key;
    }

    public string Title { get; }
    public string Key { get; }

    public override string ToString() => Title;
}
