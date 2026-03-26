namespace RenPyVisualScriptMVVM.Modules.GraphEditor.Models
{
    public sealed class GraphNote
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; } = 240;
        public double Height { get; set; } = 120;
        public string Text { get; set; } = "New note";
    }
}
