using System.Collections.Generic;

namespace RenPyVisualScriptMVVM.Modules.GraphEditor.Models
{
    public sealed class GraphViewState
    {
        public List<StoryRoute> Routes { get; set; } = new();
        public List<GraphNodePosition> NodePositions { get; set; } = new();
        public List<GraphNote> Notes { get; set; } = new();
    }
}
