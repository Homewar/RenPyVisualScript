using Avalonia;
using Avalonia.Media;
using RenPyVisualScriptMVVM.Modules.Editors.Models;
using RenPyVisualScriptMVVM.Modules.GraphEditor.Models;
using RenPyVisualScriptMVVM.Modules.Shell.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RenPyVisualScriptMVVM.Modules.GraphEditor.ViewModels;

public sealed class GraphEditorWindowViewModel : BaseViewModel
{
    private ProjectStructureSnapshot? _snapshot;

    public string Title { get; private set; } = "Graph Editor";

    public void LoadSnapshot(ProjectStructureSnapshot snapshot, string? projectName = null)
    {
        _snapshot = snapshot;
        Title = string.IsNullOrWhiteSpace(projectName)
            ? "Graph Editor"
            : $"Graph Editor — {projectName}";

        OnPropertyChanged(nameof(Title));
    }

    public (List<Node> nodes, List<Edge> edges) BuildGraph()
    {
        var nodes = new List<Node>();
        var edges = new List<Edge>();

        if (_snapshot is null || _snapshot.Labels.Count == 0)
            return (nodes, edges);

        var orderedLabels = _snapshot.Labels
            .OrderBy(l => l.FileName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(l => l.Line)
            .ToList();

        const double startX = 180;
        const double startY = 120;
        const double stepX = 260;
        const double stepY = 180;
        const int columns = 4;

        var nodeByLabel = new Dictionary<string, Node>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < orderedLabels.Count; i++)
        {
            var label = orderedLabels[i];
            var column = i % columns;
            var row = i / columns;

            var node = new Node
            {
                X = startX + column * stepX,
                Y = startY + row * stepY,
                Title = label.Name,
                Background = GetNodeBrush(label.FileName),
                ImageBackground = null,
                Size = new Size(210, 96)
            };

            nodes.Add(node);
            nodeByLabel[label.Name] = node;
        }

        foreach (var link in _snapshot.Links)
        {
            if (!nodeByLabel.TryGetValue(link.Source, out var sourceNode))
                continue;

            if (string.IsNullOrWhiteSpace(link.Target) || link.Target == "(inline branch)")
                continue;

            if (!nodeByLabel.TryGetValue(link.Target, out var targetNode))
                continue;

            if (edges.Any(e => e.Start == sourceNode && e.End == targetNode))
                continue;

            edges.Add(new Edge(sourceNode, targetNode));
        }

        return (nodes, edges);
    }

    private static IBrush GetNodeBrush(string seed)
    {
        var palette = new[]
        {
            "#4C78A8",
            "#F58518",
            "#54A24B",
            "#E45756",
            "#72B7B2",
            "#B279A2",
            "#FF9DA6",
            "#9D755D"
        };

        var index = Math.Abs(seed.GetHashCode()) % palette.Length;
        return Brush.Parse(palette[index]);
    }
}
