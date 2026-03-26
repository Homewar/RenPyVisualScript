using RenPyVisualScriptMVVM.Core.Models;
using RenPyVisualScriptMVVM.Modules.Editors.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace RenPyVisualScriptMVVM.Modules.Editors.Services;

public sealed class RenPyStructureReader
{
    private static readonly Regex CharacterRegex = new(
        "^\\s*define\\s+([A-Za-z_][A-Za-z0-9_]*)\\s*=\\s*Character\\(\\s*\"([^\"]+)\"(?<args>.*)\\)\\s*$",
        RegexOptions.Compiled);

    private static readonly Regex ColorRegex = new(
        "color\\s*=\\s*\"([^\"]+)\"",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex LabelRegex = new(
        @"^\s*label\s+([A-Za-z_][A-Za-z0-9_]*)\s*:\s*$",
        RegexOptions.Compiled);

    private static readonly Regex JumpRegex = new(
        @"^\s*jump\s+([A-Za-z_][A-Za-z0-9_]*)\b",
        RegexOptions.Compiled);

    private static readonly Regex CallRegex = new(
        @"^\s*call\s+([A-Za-z_][A-Za-z0-9_]*)\b",
        RegexOptions.Compiled);

    private static readonly Regex MenuChoiceRegex = new(
        "^\\s*\"([^\"]+)\"\\s*:\\s*$",
        RegexOptions.Compiled);

    private static readonly Regex MenuHeaderRegex = new(
        @"^\s*menu\b.*:\s*$",
        RegexOptions.Compiled);

    private static readonly HashSet<string> ExcludedFileNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "options.rpy",
        "screens.rpy",
        "gui.rpy",
        "common.rpy",
        "definitions.rpy",
        "style.rpy",
        "styles.rpy",
        "compat.rpy",
        "audio.rpy",
        "updater.rpy",
        "testcases.rpy"
    };

    private static readonly HashSet<string> ExcludedLabelNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "splashscreen",
        "before_main_menu",
        "main_menu",
        "navigation",
        "after_load"
    };

    public ProjectStructureSnapshot Read(string? projectPath)
    {
        if (string.IsNullOrWhiteSpace(projectPath) || !Directory.Exists(projectPath))
            return new ProjectStructureSnapshot(Array.Empty<Character>(), Array.Empty<LabelOutlineItem>(), Array.Empty<StructureLinkItem>());

        var normalizedProjectPath = Path.GetFullPath(projectPath);
        var characters = new List<Character>();
        var labels = new List<LabelOutlineItem>();
        var links = new List<StructureLinkItem>();

        foreach (var file in EnumerateGameScriptFiles(normalizedProjectPath))
            ParseFile(normalizedProjectPath, file, characters, labels, links);

        return new ProjectStructureSnapshot(characters, labels, links);
    }

    private static IEnumerable<string> EnumerateGameScriptFiles(string projectPath)
    {
        var allFiles = Directory.EnumerateFiles(projectPath, "*.rpy", SearchOption.AllDirectories)
            .OrderBy(static p => p, StringComparer.OrdinalIgnoreCase);

        foreach (var file in allFiles)
        {
            if (IsGameScriptFile(projectPath, file))
                yield return file;
        }
    }

    private static bool IsGameScriptFile(string projectPath, string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        if (ExcludedFileNames.Contains(fileName))
            return false;

        var normalizedProject = Path.GetFullPath(projectPath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var normalizedFile = Path.GetFullPath(filePath);
        var relativePath = Path.GetRelativePath(normalizedProject, normalizedFile)
            .Replace('\\', '/');

        if (relativePath.StartsWith("game/tl/", StringComparison.OrdinalIgnoreCase))
            return false;

        if (relativePath.StartsWith("renpy/", StringComparison.OrdinalIgnoreCase)
            || relativePath.StartsWith("common/", StringComparison.OrdinalIgnoreCase)
            || relativePath.StartsWith("launcher/", StringComparison.OrdinalIgnoreCase))
            return false;

        var segments = relativePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Any(s => s.Equals("testcases", StringComparison.OrdinalIgnoreCase)
                           || s.Equals("tests", StringComparison.OrdinalIgnoreCase)
                           || s.Equals("test", StringComparison.OrdinalIgnoreCase)))
            return false;

        return true;
    }

    private static void ParseFile(
        string projectPath,
        string filePath,
        List<Character> characters,
        List<LabelOutlineItem> labels,
        List<StructureLinkItem> links)
    {
        var lines = File.ReadAllLines(filePath);
        var relativePath = Path.GetRelativePath(projectPath, filePath).Replace('\\', '/');
        var fileName = Path.GetFileName(filePath);

        for (var i = 0; i < lines.Length; i++)
        {
            var characterMatch = CharacterRegex.Match(lines[i]);
            if (!characterMatch.Success)
                continue;

            var codeName = characterMatch.Groups[1].Value;
            var inGameName = characterMatch.Groups[2].Value;
            var args = characterMatch.Groups["args"].Value;
            var color = ExtractColor(args);
            characters.Add(new Character(codeName, color, inGameName));
        }

        var labelHeaders = new List<(string name, int startIndex)>();
        for (var i = 0; i < lines.Length; i++)
        {
            var labelMatch = LabelRegex.Match(lines[i]);
            if (labelMatch.Success)
                labelHeaders.Add((labelMatch.Groups[1].Value, i));
        }

        for (var i = 0; i < labelHeaders.Count; i++)
        {
            var (labelName, startIndex) = labelHeaders[i];
            var endIndex = i + 1 < labelHeaders.Count ? labelHeaders[i + 1].startIndex - 1 : lines.Length - 1;
            var includeCurrentLabel = IsGameLabel(labelName);
            if (!includeCurrentLabel)
                continue;

            var bodyLines = lines.Skip(startIndex + 1).Take(Math.Max(0, endIndex - startIndex)).ToList();
            var statementCount = bodyLines.Count(static line =>
            {
                var trimmed = line.Trim();
                return trimmed.Length > 0 && !trimmed.StartsWith('#');
            });

            labels.Add(new LabelOutlineItem(
                labelName,
                fileName,
                relativePath,
                statementCount,
                startIndex + 1,
                endIndex + 1,
                bodyLines));

            ParseLinksForLabel(relativePath, labelName, startIndex + 2, bodyLines, links);
        }
    }

    private static void ParseLinksForLabel(
        string relativePath,
        string currentLabel,
        int bodyStartLine,
        IReadOnlyList<string> bodyLines,
        List<StructureLinkItem> links)
    {
        int? currentMenuLine = null;
        var currentMenuIndent = -1;

        for (var i = 0; i < bodyLines.Count; i++)
        {
            var line = bodyLines[i];
            var lineNumber = bodyStartLine + i;
            var indent = GetIndentWidth(line);
            var trimmed = line.Trim();

            if (currentMenuLine.HasValue && trimmed.Length > 0 && !trimmed.StartsWith('#') && indent <= currentMenuIndent)
            {
                currentMenuLine = null;
                currentMenuIndent = -1;
            }

            if (MenuHeaderRegex.IsMatch(line))
            {
                currentMenuLine = lineNumber;
                currentMenuIndent = indent;
                continue;
            }

            var jumpMatch = JumpRegex.Match(line);
            if (jumpMatch.Success)
            {
                // Jumps inside a menu branch are already captured as the menu choice's target — skip them here.
                if (!currentMenuLine.HasValue)
                {
                    var target = jumpMatch.Groups[1].Value;
                    if (IsGameLabel(target))
                        links.Add(new StructureLinkItem("jump", currentLabel, target, $"jump → {target}", relativePath, lineNumber));
                }
                continue;
            }

            var callMatch = CallRegex.Match(line);
            if (callMatch.Success)
            {
                // Calls inside a menu branch are already captured as the menu choice's target — skip them here.
                if (!currentMenuLine.HasValue)
                {
                    var target = callMatch.Groups[1].Value;
                    if (IsGameLabel(target))
                        links.Add(new StructureLinkItem("call", currentLabel, target, $"call → {target}", relativePath, lineNumber));
                }
                continue;
            }

            var menuChoiceMatch = MenuChoiceRegex.Match(line);
            if (menuChoiceMatch.Success)
            {
                var choiceText = menuChoiceMatch.Groups[1].Value;
                var choiceIndent = indent;
                var target = TryFindMenuTarget(bodyLines, i + 1, choiceIndent);
                if (target is null || IsGameLabel(target))
                    links.Add(new StructureLinkItem("menu", currentLabel, target ?? "(inline branch)", choiceText, relativePath, lineNumber, currentMenuLine ?? lineNumber));
            }
        }
    }

    private static string? TryFindMenuTarget(IReadOnlyList<string> bodyLines, int startIndex, int choiceIndent)
    {
        for (var i = startIndex; i < bodyLines.Count; i++)
        {
            var line = bodyLines[i];
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#'))
                continue;

            var indent = GetIndentWidth(line);
            if (indent <= choiceIndent)
                return null;

            var jumpMatch = JumpRegex.Match(line);
            if (jumpMatch.Success)
                return jumpMatch.Groups[1].Value;

            var callMatch = CallRegex.Match(line);
            if (callMatch.Success)
                return callMatch.Groups[1].Value;
        }

        return null;
    }

    private static int GetIndentWidth(string line)
    {
        var count = 0;
        while (count < line.Length && char.IsWhiteSpace(line[count]))
            count++;

        return count;
    }

    private static bool IsGameLabel(string labelName)
    {
        if (string.IsNullOrWhiteSpace(labelName))
            return false;

        if (ExcludedLabelNames.Contains(labelName))
            return false;

        return !labelName.StartsWith("_", StringComparison.Ordinal);
    }

    private static string ExtractColor(string args)
    {
        var match = ColorRegex.Match(args);
        if (!match.Success)
            return "";

        return match.Groups[1].Value;
    }
}
