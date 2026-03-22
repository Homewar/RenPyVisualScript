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
    public string? ProjectPath { get; private set; }
    public ProjectStructureSnapshot? Snapshot => _snapshot;

    public void LoadSnapshot(ProjectStructureSnapshot snapshot, string? projectName = null, string? projectPath = null)
    {
        _snapshot = snapshot;
        ProjectPath = projectPath;
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
            .OrderBy(l => l.FilePath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(l => l.Line)
            .ToList();

        const double nodeWidth  = 210;
        const double nodeHeight = 96;
        const double columnGap  = 80;   // horizontal gap between node edges
        const double startX     = 180;
        const double startY     = 120;

        var nodeByLabel = new Dictionary<string, Node>(StringComparer.OrdinalIgnoreCase);

        // ── 1. Create all nodes (position TBD) ──────────────────────────────
        foreach (var label in orderedLabels)
        {
            var node = new Node
            {
                Title             = label.Name,
                Background        = GetNodeBrush(label.FilePath),
                ImageBackground   = null,
                Size              = new Size(nodeWidth, nodeHeight),
                IsGeneratedManually = false,
                SourceFilePath    = label.FilePath,
                SourceStartLine   = label.Line,
                SourceEndLine     = label.EndLine,
                BodyLines         = label.BodyLines.ToList()
            };
            nodes.Add(node);
            nodeByLabel[label.Name] = node;
        }

        // ── 2. Build edge list ───────────────────────────────────────────────
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

        // ── 3. Tree layout ───────────────────────────────────────────────────
        // Build adjacency: children[node] = ordered list of direct successors
        var children = nodes.ToDictionary(n => n, _ => new List<Node>());
        var inDegree  = nodes.ToDictionary(n => n, _ => 0);

        foreach (var edge in edges)
        {
            if (edge.Start == edge.End) continue;          // self-loops don't count
            children[edge.Start].Add(edge.End);
            inDegree[edge.End]++;
        }

        // Roots = nodes with in-degree 0 (or all nodes if graph has no roots)
        var roots = nodes.Where(n => inDegree[n] == 0).ToList();
        if (roots.Count == 0) roots = nodes.Take(1).ToList();

        // Assign tree columns (depth) via BFS to avoid revisiting
        var depth    = new Dictionary<Node, int>();
        var queue    = new Queue<Node>();
        foreach (var root in roots) { depth[root] = 0; queue.Enqueue(root); }

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var child in children[current])
            {
                if (!depth.ContainsKey(child))
                {
                    depth[child] = depth[current] + 1;
                    queue.Enqueue(child);
                }
            }
        }

        // Any node not reachable from roots gets its own column
        foreach (var node in nodes)
            if (!depth.ContainsKey(node))
                depth[node] = 0;

        // Group nodes by column
        var byColumn = nodes.GroupBy(n => depth[n])
                            .OrderBy(g => g.Key)
                            .ToList();

        // Assign X centre for each column
        var columnCenterX = new Dictionary<int, double>();
        double x = startX;
        foreach (var col in byColumn)
        {
            columnCenterX[col.Key] = x;
            x += nodeWidth + columnGap;
        }

        // Assign Y centres: children of a node are stacked with 1.1 × nodeHeight spacing,
        // then the parent is centred on its children group.
        // We do a post-order DFS to assign Y bottom-up.
        var assignedY = new Dictionary<Node, double>();
        // Track the next available Y per column (used for nodes with no children yet placed)
        var nextY = new Dictionary<int, double>();
        foreach (var col in byColumn) nextY[col.Key] = startY;

        void AssignY(Node node, HashSet<Node> visiting)
        {
            if (assignedY.ContainsKey(node)) return;
            if (!visiting.Add(node)) return;   // cycle guard

            var childList = children[node]
                .Where(c => c != node)         // skip self-loops
                .ToList();

            // Recurse into children first
            foreach (var child in childList)
                AssignY(child, visiting);

            if (childList.Count > 0 && childList.All(c => assignedY.ContainsKey(c)))
            {
                // Centre parent on its children
                double minChildY = childList.Min(c => assignedY[c]);
                double maxChildY = childList.Max(c => assignedY[c]);
                double parentY   = (minChildY + maxChildY) / 2.0;

                // Make sure this Y doesn't collide with already-placed nodes in same column
                int col = depth[node];
                double minY = nextY.ContainsKey(col) ? nextY[col] : startY;
                parentY = Math.Max(parentY, minY);

                assignedY[node] = parentY;
                nextY[col] = parentY + nodeHeight * 1.1;
            }
            else
            {
                // Leaf (or children not yet known): use nextY for this column
                int col = depth[node];
                double y2 = nextY.ContainsKey(col) ? nextY[col] : startY;
                assignedY[node] = y2;
                nextY[col] = y2 + nodeHeight * 1.1;
            }

            visiting.Remove(node);
        }

        // Process roots first, then any remaining nodes
        var visiting = new HashSet<Node>();
        foreach (var root in roots) AssignY(root, visiting);
        foreach (var node in nodes)  AssignY(node, visiting);

        // Apply positions
        foreach (var node in nodes)
        {
            node.X = columnCenterX[depth[node]];
            node.Y = assignedY.ContainsKey(node) ? assignedY[node] : startY;
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
