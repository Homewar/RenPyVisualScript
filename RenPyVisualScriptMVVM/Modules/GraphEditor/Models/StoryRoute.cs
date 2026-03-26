using System.Collections.Generic;

namespace RenPyVisualScriptMVVM.Modules.GraphEditor.Models
{
    public sealed class StoryRoute
    {
        public string Name { get; set; } = string.Empty;
        public List<string> NodeTitles { get; set; } = new();
    }
}
