using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using RenPyVisualScriptMVVM.Modules.Editors.Models;
using RenPyVisualScriptMVVM.Modules.Editors.Services;
using RenPyVisualScriptMVVM.Modules.GraphEditor.Models;

namespace RenPyVisualScriptMVVM.Modules.GraphEditor.Services;

public sealed class GraphRpyExportResult
{
    public required IReadOnlyList<string> UpdatedFiles { get; init; }
    public required int SyncedNodeCount { get; init; }
    public required int DeletedNodeCount { get; init; }
    public required int SyncedEdgeCount { get; init; }
    public required IReadOnlyDictionary<string, string> RenamedNodeMap { get; init; }
}

public static class GraphRpyExporter
{
    private const string GraphBeginMarker = "# graph-sync:begin";
    private const string GraphEndMarker = "# graph-sync:end";
    private static readonly Regex JumpRegex = new(@"^\s*jump\s+[A-Za-z_][A-Za-z0-9_]*\b", RegexOptions.Compiled);
    private static readonly Regex CallRegex = new(@"^\s*call\s+[A-Za-z_][A-Za-z0-9_]*\b", RegexOptions.Compiled);
    private static readonly Regex ReturnRegex = new(@"^\s*return\b", RegexOptions.Compiled);
    private static readonly Regex ValidLabelRegex = new(@"^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled);

    public static GraphRpyExportResult SynchronizeGraph(
        string? projectPath,
        ProjectStructureSnapshot? initialSnapshot,
        IEnumerable<Node> nodes,
        IEnumerable<Edge> edges)
    {
        if (string.IsNullOrWhiteSpace(projectPath))
            throw new InvalidOperationException("Не задан путь к проекту.");

        if (initialSnapshot is null)
            throw new InvalidOperationException("Нет исходного снимка структуры проекта.");

        var normalizedProjectPath = Path.GetFullPath(projectPath);
        var structureReader = new RenPyStructureReader();
        var currentSnapshot = structureReader.Read(normalizedProjectPath);

        var nodeList = nodes.ToList();
        var edgeList = edges.ToList();
        var renamedNodeMap = NormalizeManualNodeTitles(nodeList, initialSnapshot, currentSnapshot);

        var initialLabelNames = new HashSet<string>(initialSnapshot.Labels.Select(l => l.Name), StringComparer.OrdinalIgnoreCase);
        var currentNodeNames = new HashSet<string>(nodeList.Select(n => n.Title), StringComparer.OrdinalIgnoreCase);
        var deletedLabelNames = new HashSet<string>(
            initialSnapshot.Labels
                .Select(l => l.Name)
                .Where(name => !currentNodeNames.Contains(name)),
            StringComparer.OrdinalIgnoreCase);

        var labelByName = currentSnapshot.Labels.ToDictionary(l => l.Name, StringComparer.OrdinalIgnoreCase);
        var targetFileByNode = ResolveTargetFiles(nodeList, edgeList, initialSnapshot, currentSnapshot, labelByName);
        var renderedBlockByLabel = nodeList.ToDictionary(
            n => n.Title,
            n => RenderLabelBlock(n, edgeList.Where(e => e.Start == n).Select(e => e.End).Distinct().ToList()),
            StringComparer.OrdinalIgnoreCase);

        var filesToUpdate = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var label in initialSnapshot.Labels)
            filesToUpdate.Add(label.FilePath);
        foreach (var file in targetFileByNode.Values)
            filesToUpdate.Add(file);

        var updatedFiles = new List<string>();
        foreach (var relativeFilePath in filesToUpdate.OrderBy(static p => p, StringComparer.OrdinalIgnoreCase))
        {
            var absoluteFilePath = Path.Combine(normalizedProjectPath, relativeFilePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(absoluteFilePath) ?? normalizedProjectPath);

            var currentLines = File.Exists(absoluteFilePath)
                ? File.ReadAllLines(absoluteFilePath).ToList()
                : new List<string>();

            var labelsInFile = currentSnapshot.Labels
                .Where(l => string.Equals(l.FilePath, relativeFilePath, StringComparison.OrdinalIgnoreCase))
                .OrderBy(l => l.Line)
                .ToList();

            var outputLines = new List<string>();
            var cursor = 1;

            foreach (var label in labelsInFile)
            {
                AppendRange(outputLines, currentLines, cursor, label.Line - 1);

                if (deletedLabelNames.Contains(label.Name))
                {
                    cursor = label.EndLine + 1;
                    continue;
                }

                if (renderedBlockByLabel.TryGetValue(label.Name, out var renderedBlock)
                    && targetFileByNode.TryGetValue(label.Name, out var targetFile)
                    && string.Equals(targetFile, relativeFilePath, StringComparison.OrdinalIgnoreCase))
                {
                    outputLines.AddRange(renderedBlock);
                }
                else
                {
                    AppendRange(outputLines, currentLines, label.Line, label.EndLine);
                }

                cursor = label.EndLine + 1;
            }

            AppendRange(outputLines, currentLines, cursor, currentLines.Count);

            var newNodesForFile = nodeList
                .Where(n => !labelByName.ContainsKey(n.Title))
                .Where(n => targetFileByNode.TryGetValue(n.Title, out var targetFile)
                            && string.Equals(targetFile, relativeFilePath, StringComparison.OrdinalIgnoreCase))
                .OrderBy(n => n.Title, StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var node in newNodesForFile)
            {
                EnsureSeparatedByBlankLine(outputLines);
                outputLines.AddRange(renderedBlockByLabel[node.Title]);
            }

            var newContent = string.Join(Environment.NewLine, outputLines);
            if (outputLines.Count > 0)
                newContent += Environment.NewLine;

            var oldContent = File.Exists(absoluteFilePath)
                ? File.ReadAllText(absoluteFilePath)
                : string.Empty;

            if (!string.Equals(oldContent, newContent, StringComparison.Ordinal))
            {
                File.WriteAllText(absoluteFilePath, newContent, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                updatedFiles.Add(relativeFilePath);
            }
        }

        return new GraphRpyExportResult
        {
            UpdatedFiles = updatedFiles,
            SyncedNodeCount = nodeList.Count,
            DeletedNodeCount = deletedLabelNames.Count,
            SyncedEdgeCount = edgeList.Count,
            RenamedNodeMap = renamedNodeMap
        };
    }

    private static Dictionary<string, string> NormalizeManualNodeTitles(
        IReadOnlyCollection<Node> nodeList,
        ProjectStructureSnapshot initialSnapshot,
        ProjectStructureSnapshot currentSnapshot)
    {
        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var label in initialSnapshot.Labels)
            usedNames.Add(label.Name);
        foreach (var label in currentSnapshot.Labels)
            usedNames.Add(label.Name);

        foreach (var node in nodeList.Where(n => !n.IsGeneratedManually))
            usedNames.Remove(node.Title);

        var renamedNodeMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var node in nodeList.Where(n => n.IsGeneratedManually))
        {
            var originalTitle = node.Title;
            var normalized = MakeValidUniqueLabel(originalTitle, usedNames);
            usedNames.Add(normalized);
            node.Title = normalized;
            if (!renamedNodeMap.ContainsKey(originalTitle) && !string.Equals(originalTitle, normalized, StringComparison.Ordinal))
                renamedNodeMap[originalTitle] = normalized;
        }

        return renamedNodeMap;
    }

    private static Dictionary<string, string> ResolveTargetFiles(
        IReadOnlyCollection<Node> nodeList,
        IReadOnlyCollection<Edge> edgeList,
        ProjectStructureSnapshot initialSnapshot,
        ProjectStructureSnapshot currentSnapshot,
        IReadOnlyDictionary<string, LabelOutlineItem> currentLabelsByName)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var fallbackFile = ResolveFallbackFile(initialSnapshot, currentSnapshot);

        foreach (var node in nodeList)
        {
            if (!node.IsGeneratedManually)
            {
                if (!string.IsNullOrWhiteSpace(node.SourceFilePath))
                {
                    result[node.Title] = node.SourceFilePath!;
                    continue;
                }

                if (currentLabelsByName.TryGetValue(node.Title, out var currentLabel))
                {
                    result[node.Title] = currentLabel.FilePath;
                    continue;
                }

                var initialLabel = initialSnapshot.Labels.FirstOrDefault(l => string.Equals(l.Name, node.Title, StringComparison.OrdinalIgnoreCase));
                if (initialLabel != null)
                {
                    result[node.Title] = initialLabel.FilePath;
                    continue;
                }
            }

            var incomingFile = edgeList
                .Where(e => e.End == node && !e.Start.IsGeneratedManually && !string.IsNullOrWhiteSpace(e.Start.SourceFilePath))
                .Select(e => e.Start.SourceFilePath!)
                .GroupBy(static x => x, StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(static g => g.Count())
                .ThenBy(static g => g.Key, StringComparer.OrdinalIgnoreCase)
                .Select(static g => g.Key)
                .FirstOrDefault();

            if (!string.IsNullOrWhiteSpace(incomingFile))
            {
                result[node.Title] = incomingFile;
                continue;
            }

            var outgoingFile = edgeList
                .Where(e => e.Start == node && !e.End.IsGeneratedManually && !string.IsNullOrWhiteSpace(e.End.SourceFilePath))
                .Select(e => e.End.SourceFilePath!)
                .GroupBy(static x => x, StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(static g => g.Count())
                .ThenBy(static g => g.Key, StringComparer.OrdinalIgnoreCase)
                .Select(static g => g.Key)
                .FirstOrDefault();

            result[node.Title] = !string.IsNullOrWhiteSpace(outgoingFile) ? outgoingFile : fallbackFile;
        }

        return result;
    }

    private static string ResolveFallbackFile(ProjectStructureSnapshot initialSnapshot, ProjectStructureSnapshot currentSnapshot)
    {
        var firstKnownFile = initialSnapshot.Labels.FirstOrDefault()?.FilePath
                             ?? currentSnapshot.Labels.FirstOrDefault()?.FilePath;

        if (!string.IsNullOrWhiteSpace(firstKnownFile))
            return firstKnownFile!;

        return Path.Combine("game", "script.rpy").Replace('\\', '/');
    }

    private static List<string> RenderLabelBlock(Node node, IReadOnlyList<Node> outgoingNodes)
    {
        var staticBody = PrepareStaticBodyLines(node);
        var flowLines = BuildFlowLines(outgoingNodes);
        var block = new List<string> { $"label {node.Title}:" };

        if (staticBody.Count > 0)
            block.AddRange(staticBody);

        if (staticBody.Count > 0 && staticBody[^1].Trim().Length > 0)
            block.Add(string.Empty);

        block.Add($"    {GraphBeginMarker}");
        block.AddRange(flowLines);
        block.Add($"    {GraphEndMarker}");
        block.Add(string.Empty);

        return block;
    }

    private static List<string> PrepareStaticBodyLines(Node node)
    {
        var bodyLines = (node.BodyLines ?? new List<string>()).ToList();
        RemoveManagedGraphRegion(bodyLines);
        TrimTrailingBlankLines(bodyLines);
        RemoveTrailingFlowBlock(bodyLines);
        TrimTrailingBlankLines(bodyLines);

        if (bodyLines.Count == 0 && node.IsGeneratedManually)
            bodyLines.Add($"    \"TODO: {EscapeString(node.Title)}\"");

        return bodyLines;
    }

    private static void RemoveManagedGraphRegion(List<string> bodyLines)
    {
        var beginIndex = bodyLines.FindIndex(static line => line.Contains(GraphBeginMarker, StringComparison.Ordinal));
        var endIndex = bodyLines.FindIndex(static line => line.Contains(GraphEndMarker, StringComparison.Ordinal));
        if (beginIndex < 0 || endIndex < beginIndex)
            return;

        bodyLines.RemoveRange(beginIndex, endIndex - beginIndex + 1);
    }

    private static void RemoveTrailingFlowBlock(List<string> bodyLines)
    {
        TrimTrailingBlankLines(bodyLines);
        if (bodyLines.Count == 0)
            return;

        var lastIndex = bodyLines.Count - 1;
        var lastTrimmed = bodyLines[lastIndex].Trim();
        if (JumpRegex.IsMatch(lastTrimmed) || CallRegex.IsMatch(lastTrimmed) || ReturnRegex.IsMatch(lastTrimmed))
        {
            bodyLines.RemoveAt(lastIndex);
            return;
        }

        for (var i = bodyLines.Count - 1; i >= 0; i--)
        {
            var trimmed = bodyLines[i].Trim();
            if (trimmed.Length == 0)
                continue;

            if (GetIndentWidth(bodyLines[i]) == 4 && string.Equals(trimmed, "menu:", StringComparison.Ordinal))
            {
                var isTerminalMenu = true;
                for (var j = i + 1; j < bodyLines.Count; j++)
                {
                    var childTrimmed = bodyLines[j].Trim();
                    if (childTrimmed.Length == 0)
                        continue;

                    if (GetIndentWidth(bodyLines[j]) <= 4)
                    {
                        isTerminalMenu = false;
                        break;
                    }
                }

                if (isTerminalMenu)
                    bodyLines.RemoveRange(i, bodyLines.Count - i);

                return;
            }

            if (GetIndentWidth(bodyLines[i]) <= 4)
                return;
        }
    }

    private static List<string> BuildFlowLines(IReadOnlyList<Node> outgoingNodes)
    {
        var distinctTargets = outgoingNodes
            .Distinct()
            .OrderBy(n => n.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (distinctTargets.Count == 0)
            return new List<string> { "    return" };

        if (distinctTargets.Count == 1)
            return new List<string> { $"    jump {distinctTargets[0].Title}" };

        var lines = new List<string> { "    menu:" };
        foreach (var target in distinctTargets)
        {
            lines.Add($"        \"Перейти в {EscapeString(target.Title)}\":" );
            lines.Add($"            jump {target.Title}");
        }

        return lines;
    }

    private static string MakeValidUniqueLabel(string? rawTitle, HashSet<string> usedNames)
    {
        var candidate = string.IsNullOrWhiteSpace(rawTitle) ? "generated_node" : rawTitle.Trim();
        if (!ValidLabelRegex.IsMatch(candidate))
            candidate = SanitizeLabel(candidate);

        if (!usedNames.Contains(candidate))
            return candidate;

        var suffix = 2;
        while (usedNames.Contains($"{candidate}_{suffix}"))
            suffix++;

        return $"{candidate}_{suffix}";
    }

    private static string SanitizeLabel(string value)
    {
        var normalized = value.ToLowerInvariant();
        normalized = Regex.Replace(normalized, "[^a-z0-9_]+", "_");
        normalized = Regex.Replace(normalized, "_+", "_").Trim('_');

        if (string.IsNullOrWhiteSpace(normalized))
            normalized = "generated_node";

        if (char.IsDigit(normalized[0]))
            normalized = $"node_{normalized}";

        return normalized;
    }

    private static void EnsureSeparatedByBlankLine(List<string> lines)
    {
        while (lines.Count > 0 && lines[^1].Length == 0)
            lines.RemoveAt(lines.Count - 1);

        if (lines.Count > 0)
            lines.Add(string.Empty);
    }

    private static void AppendRange(List<string> target, IReadOnlyList<string> source, int startLineInclusive, int endLineInclusive)
    {
        if (source.Count == 0 || startLineInclusive > endLineInclusive)
            return;

        var startIndex = Math.Max(0, startLineInclusive - 1);
        var endIndex = Math.Min(source.Count - 1, endLineInclusive - 1);
        for (var i = startIndex; i <= endIndex; i++)
            target.Add(source[i]);
    }

    private static void TrimTrailingBlankLines(List<string> lines)
    {
        while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[^1]))
            lines.RemoveAt(lines.Count - 1);
    }

    private static int GetIndentWidth(string line)
    {
        var count = 0;
        while (count < line.Length && char.IsWhiteSpace(line[count]))
            count++;

        return count;
    }

    private static string EscapeString(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"");
}
