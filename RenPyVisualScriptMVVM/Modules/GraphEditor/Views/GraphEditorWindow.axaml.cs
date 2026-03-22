using System;
using System.Linq;
using Avalonia.Controls;
using RenPyVisualScriptMVVM.Modules.GraphEditor.ViewModels;
using RenPyVisualScriptMVVM.Modules.GraphEditor.Services;
using RenPyVisualScriptMVVM.Modules.Shell.Services.Interfaces;
using RenPyVisualScriptMVVM.Modules.Shell.ViewModels;
using Splat;

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

        private async void ExportNewNodesToRpy_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (DataContext is not GraphEditorWindowViewModel vm)
                return;

            try
            {
                var result = GraphRpyExporter.SynchronizeGraph(vm.ProjectPath, vm.Snapshot, GraphCanvas.Nodes, GraphCanvas.Edges);
                var windows = Locator.Current.GetService<IWindowService>();
                if (windows != null)
                {
                    var fileList = result.UpdatedFiles.Count == 0
                        ? "Файлы не изменились."
                        : string.Join("\n", result.UpdatedFiles.Select(path => $"• {path}"));

                    var renameInfo = result.RenamedNodeMap.Count == 0
                        ? string.Empty
                        : "\n\nПереименованы узлы:\n" + string.Join("\n", result.RenamedNodeMap.Select(pair => $"• {pair.Key} → {pair.Value}"));

                    await windows.ShowDialogAsync(new MessageDialogViewModel(
                        "Синхронизация завершена",
                        $"Обновлено файлов: {result.UpdatedFiles.Count}\nСинхронизировано узлов: {result.SyncedNodeCount}\nУдалено label: {result.DeletedNodeCount}\nСвязей обработано: {result.SyncedEdgeCount}\n\n{fileList}{renameInfo}"));
                }
            }
            catch (Exception ex)
            {
                var windows = Locator.Current.GetService<IWindowService>();
                if (windows != null)
                {
                    await windows.ShowDialogAsync(new MessageDialogViewModel(
                        "Ошибка синхронизации",
                        ex.Message));
                }
            }
        }
    }
}
