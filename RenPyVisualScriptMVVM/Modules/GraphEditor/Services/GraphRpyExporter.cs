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
    private static readonly Regex StaticTransferRegex = new(
        @"^\s*(?:(?<kind>jump|call)\s+(?<target>[A-Za-z_][A-Za-z0-9_]*)\b|(?<kind>return)\b)",
        RegexOptions.Compiled);
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

        var nodeList = nodes.Where(IsExportableNode).ToList();
        var exportableNodes = new HashSet<Node>(nodeList);
        var edgeList = edges
            .Where(edge => exportableNodes.Contains(edge.Start) && exportableNodes.Contains(edge.End))
            .ToList();
        var renamedNodeMap = NormalizeManualNodeTitles(nodeList, initialSnapshot, currentSnapshot);

        var initialLabelNames = new HashSet<string>(initialSnapshot.Labels.Select(l => l.Name), StringComparer.OrdinalIgnoreCase);
        var currentNodeNames = new HashSet<string>(nodeList.Select(n => n.Title), StringComparer.OrdinalIgnoreCase);
        var deletedLabelNames = new HashSet<string>(
            initialSnapshot.Labels
                .Select(l => l.Name)
                .Where(name => !currentNodeNames.Contains(name)),
            StringComparer.OrdinalIgnoreCase);

        var labelByName = currentSnapshot.Labels.ToDictionary(l => l.Name, StringComparer.OrdinalIgnoreCase);
        var targetFileByNode = ResolveTargetFiles(nodeList, edgeList, initialSnapshot, currentSnapshot, labelByName, normalizedProjectPath);
        var renderedBlockByLabel = nodeList.ToDictionary(
            n => n.Title,
            n => RenderLabelBlock(n, edgeList.Where(e => e.Start == n).Select(e => e.End).ToList()),
            StringComparer.OrdinalIgnoreCase);

        var filesToUpdate = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var label in initialSnapshot.Labels)
            filesToUpdate.Add(label.FilePath);
        foreach (var file in targetFileByNode.Values)
            filesToUpdate.Add(file);

        var updatedFiles = new List<string>();
        var actualDeletedNodeCount = 0;
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
                var isManagedLabel = HasManagedGraphRegion(label);
                var hasGraphFlowChanges = HasGraphFlowChanges(label.Name, edgeList, initialSnapshot);

                if (deletedLabelNames.Contains(label.Name) && isManagedLabel)
                {
                    actualDeletedNodeCount++;
                    cursor = label.EndLine + 1;
                    continue;
                }

                if (renderedBlockByLabel.TryGetValue(label.Name, out var renderedBlock)
                    && targetFileByNode.TryGetValue(label.Name, out var targetFile)
                    && string.Equals(targetFile, relativeFilePath, StringComparison.OrdinalIgnoreCase)
                    && (isManagedLabel || hasGraphFlowChanges))
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
            DeletedNodeCount = actualDeletedNodeCount,
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
        IReadOnlyDictionary<string, LabelOutlineItem> currentLabelsByName,
        string projectPath)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var fallbackFile = ResolveFallbackFile(initialSnapshot, currentSnapshot);

        foreach (var node in nodeList)
        {
            if (!node.IsGeneratedManually)
            {
                if (!string.IsNullOrWhiteSpace(node.SourceFilePath))
                {
                    result[node.Title] = NormalizeProjectRelativePath(projectPath, node.SourceFilePath!);
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
                .Select(e => NormalizeProjectRelativePath(projectPath, e.Start.SourceFilePath!))
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
                .Select(e => NormalizeProjectRelativePath(projectPath, e.End.SourceFilePath!))
                .GroupBy(static x => x, StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(static g => g.Count())
                .ThenBy(static g => g.Key, StringComparer.OrdinalIgnoreCase)
                .Select(static g => g.Key)
                .FirstOrDefault();

            result[node.Title] = !string.IsNullOrWhiteSpace(outgoingFile) ? outgoingFile : fallbackFile;
        }

        return result;
    }

    private static bool IsExportableNode(Node node)
    {
        return !node.IsScreenConnector && !node.IsMenuConnector;
    }

    private static string NormalizeProjectRelativePath(string projectPath, string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return filePath;

        var normalizedFilePath = filePath.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
        if (!Path.IsPathRooted(normalizedFilePath))
            return normalizedFilePath.Replace('\\', '/');

        var relativePath = Path.GetRelativePath(projectPath, normalizedFilePath);
        return relativePath.Replace('\\', '/');
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
        var flowLines = BuildFlowLines(outgoingNodes);
        var staticBody = PrepareStaticBodyLines(node, flowLines);
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

    private static List<string> PrepareStaticBodyLines(Node node, IReadOnlyList<string> newFlowLines)
    {
        var bodyLines = (node.BodyLines ?? new List<string>()).ToList();
        var oldManagedFlowLines = ExtractManagedGraphFlowLines(bodyLines);
        var newTargetNames = ExtractFlowTargetNames(newFlowLines);
        RemoveManagedGraphRegion(bodyLines);
        TrimTrailingBlankLines(bodyLines);
        CommentRemovedStaticTransfers(bodyLines, newTargetNames);
        CommentTrailingFlowBlock(bodyLines);
        AddCommentedOldManagedFlow(bodyLines, oldManagedFlowLines, newFlowLines);
        RemoveDuplicateGeneratedFlowComments(bodyLines);
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

    private static List<string> ExtractManagedGraphFlowLines(IReadOnlyList<string> bodyLines)
    {
        var beginIndex = bodyLines.ToList().FindIndex(static line => line.Contains(GraphBeginMarker, StringComparison.Ordinal));
        var endIndex = bodyLines.ToList().FindIndex(static line => line.Contains(GraphEndMarker, StringComparison.Ordinal));
        if (beginIndex < 0 || endIndex <= beginIndex)
            return new List<string>();

        return bodyLines
            .Skip(beginIndex + 1)
            .Take(endIndex - beginIndex - 1)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();
    }

    private static void AddCommentedOldManagedFlow(
        List<string> bodyLines,
        IReadOnlyList<string> oldManagedFlowLines,
        IReadOnlyList<string> newFlowLines)
    {
        if (oldManagedFlowLines.Count == 0 || FlowLinesEquivalent(oldManagedFlowLines, newFlowLines))
            return;

        RemoveTrailingGeneratedFlowCommentBlock(bodyLines);
        TrimTrailingBlankLines(bodyLines);
        if (bodyLines.Count > 0)
            bodyLines.Add(string.Empty);

        foreach (var line in oldManagedFlowLines)
        {
            if (IsCommentedRenPyLine(line))
                continue;

            var commentedLine = CommentRenPyLine(line);
            if (!ContainsEquivalentLine(bodyLines, commentedLine))
                bodyLines.Add(commentedLine);
        }
    }

    private static void RemoveDuplicateGeneratedFlowComments(List<string> bodyLines)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var i = bodyLines.Count - 1; i >= 0; i--)
        {
            var normalized = NormalizeGeneratedFlowComment(bodyLines[i]);
            if (normalized.Length == 0)
                continue;

            if (!seen.Add(normalized))
                bodyLines.RemoveAt(i);
        }
    }

    private static void RemoveTrailingGeneratedFlowCommentBlock(List<string> bodyLines)
    {
        TrimTrailingBlankLines(bodyLines);

        var startIndex = bodyLines.Count;
        for (var i = bodyLines.Count - 1; i >= 0; i--)
        {
            if (string.IsNullOrWhiteSpace(bodyLines[i]))
            {
                startIndex = i;
                continue;
            }

            if (NormalizeGeneratedFlowComment(bodyLines[i]).Length == 0)
                break;

            startIndex = i;
        }

        if (startIndex >= bodyLines.Count)
            return;

        bodyLines.RemoveRange(startIndex, bodyLines.Count - startIndex);
    }

    private static string NormalizeGeneratedFlowComment(string line)
    {
        var trimmed = line.TrimStart();
        if (!trimmed.StartsWith("#", StringComparison.Ordinal))
            return string.Empty;

        var content = trimmed[1..].TrimStart();
        if (JumpRegex.IsMatch(content)
            || CallRegex.IsMatch(content)
            || ReturnRegex.IsMatch(content)
            || string.Equals(content, "menu:", StringComparison.Ordinal)
            || content.StartsWith("\"", StringComparison.Ordinal))
        {
            return content.Trim();
        }

        return string.Empty;
    }

    private static bool ContainsEquivalentLine(IEnumerable<string> lines, string candidate)
    {
        var normalizedCandidate = candidate.Trim();
        return lines.Any(line => string.Equals(line.Trim(), normalizedCandidate, StringComparison.Ordinal));
    }

    private static bool FlowLinesEquivalent(IReadOnlyList<string> left, IReadOnlyList<string> right)
    {
        var normalizedLeft = left
            .Where(static line => !string.IsNullOrWhiteSpace(line))
            .Select(NormalizeFlowLine)
            .Where(static line => line.Length > 0)
            .ToList();

        var normalizedRight = right
            .Where(static line => !string.IsNullOrWhiteSpace(line))
            .Select(NormalizeFlowLine)
            .Where(static line => line.Length > 0)
            .ToList();

        return normalizedLeft.SequenceEqual(normalizedRight, StringComparer.Ordinal);
    }

    private static HashSet<string> ExtractFlowTargetNames(IEnumerable<string> flowLines)
    {
        var targets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in flowLines)
        {
            var match = StaticTransferRegex.Match(line);
            if (match.Success && match.Groups["target"].Success)
                targets.Add(match.Groups["target"].Value);
        }

        return targets;
    }

    private static void CommentRemovedStaticTransfers(List<string> bodyLines, IReadOnlySet<string> newTargetNames)
    {
        for (var i = 0; i < bodyLines.Count; i++)
        {
            var line = bodyLines[i];
            if (IsCommentedRenPyLine(line))
                continue;

            var match = StaticTransferRegex.Match(line);
            if (!match.Success || !match.Groups["target"].Success)
                continue;

            var target = match.Groups["target"].Value;
            if (!newTargetNames.Contains(target))
                bodyLines[i] = CommentRenPyLine(line);
        }
    }

    private static string NormalizeFlowLine(string line)
    {
        var trimmed = line.Trim();
        return trimmed.StartsWith("#", StringComparison.Ordinal)
            ? string.Empty
            : trimmed;
    }

    private static bool HasManagedGraphRegion(LabelOutlineItem label)
    {
        var beginIndex = label.BodyLines
            .ToList()
            .FindIndex(static line => line.Contains(GraphBeginMarker, StringComparison.Ordinal));
        if (beginIndex < 0)
            return false;

        var endIndex = label.BodyLines
            .ToList()
            .FindIndex(static line => line.Contains(GraphEndMarker, StringComparison.Ordinal));
        return endIndex > beginIndex;
    }

    private static bool HasGraphFlowChanges(
        string labelName,
        IReadOnlyCollection<Edge> edgeList,
        ProjectStructureSnapshot initialSnapshot)
    {
        var currentTargets = edgeList
            .Where(edge => string.Equals(edge.Start.Title, labelName, StringComparison.OrdinalIgnoreCase))
            .Select(edge => edge.End.Title)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var initialTargets = initialSnapshot.Links
            .Where(link => string.Equals(link.Source, labelName, StringComparison.OrdinalIgnoreCase))
            .Where(link => IsExportableStructureLink(link))
            .Select(link => link.Target)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return !currentTargets.SetEquals(initialTargets);
    }

    private static bool IsExportableStructureLink(StructureLinkItem link)
    {
        if (string.Equals(link.Kind, "fallthrough", StringComparison.OrdinalIgnoreCase))
            return false;

        if (!string.IsNullOrWhiteSpace(link.ScreenName) || !string.IsNullOrWhiteSpace(link.MenuName))
            return false;

        return !string.IsNullOrWhiteSpace(link.Target)
               && !string.Equals(link.Target, "(inline branch)", StringComparison.OrdinalIgnoreCase);
    }

    private static void CommentTrailingFlowBlock(List<string> bodyLines)
    {
        TrimTrailingBlankLines(bodyLines);
        if (bodyLines.Count == 0)
            return;

        var lastIndex = bodyLines.Count - 1;
        var lastTrimmed = bodyLines[lastIndex].Trim();
        if (JumpRegex.IsMatch(lastTrimmed) || CallRegex.IsMatch(lastTrimmed) || ReturnRegex.IsMatch(lastTrimmed))
        {
            bodyLines[lastIndex] = CommentRenPyLine(bodyLines[lastIndex]);
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
                {
                    for (var j = i; j < bodyLines.Count; j++)
                    {
                        if (!string.IsNullOrWhiteSpace(bodyLines[j]))
                            bodyLines[j] = CommentRenPyLine(bodyLines[j]);
                    }
                }

                return;
            }

            if (GetIndentWidth(bodyLines[i]) <= 4)
                return;
        }
    }

    private static List<string> BuildFlowLines(IReadOnlyList<Node> outgoingNodes)
    {
        var distinctTargets = outgoingNodes
            .GroupBy(n => n.Title, StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.First())
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

    private static string CommentRenPyLine(string line)
    {
        var indentLength = GetIndentWidth(line);
        var indent = line[..indentLength];
        var content = line[indentLength..];
        return IsCommentedRenPyLine(line)
            ? line
            : $"{indent}# {content}";
    }

    private static bool IsCommentedRenPyLine(string line)
    {
        var indentLength = GetIndentWidth(line);
        return line[indentLength..].TrimStart().StartsWith("#", StringComparison.Ordinal);
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
