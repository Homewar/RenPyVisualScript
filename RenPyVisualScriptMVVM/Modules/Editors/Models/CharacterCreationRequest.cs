namespace RenPyVisualScriptMVVM.Modules.Editors.Models;

public sealed class CharacterCreationRequest
{
    public string CodeName { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Color { get; set; } = "";
    public string? ImageTag { get; set; }
    public string? ImageSourcePath { get; set; }
}
