using Avalonia.Controls;
using RenPyVisualScriptMVVM.Modules.GraphEditor.ViewModels;

namespace RenPyVisualScriptMVVM.Modules.GraphEditor.Views
{
    public partial class GraphEditorWindow : Window
    {
        public GraphEditorWindow()
        {
            InitializeComponent();
            Opened += (_, _) => ApplyGraph();
            DataContextChanged += (_, _) => ApplyGraph();
        }

        private void ApplyGraph()
        {
            if (DataContext is not GraphEditorWindowViewModel vm)
                return;

            Title = vm.Title;

            var (nodes, edges) = vm.BuildGraph();
            GraphCanvas.Nodes.Clear();
            GraphCanvas.Edges.Clear();
            GraphCanvas.Nodes.AddRange(nodes);
            GraphCanvas.Edges.AddRange(edges);
            GraphCanvas.RebuildChildren();
        }
    }
}
