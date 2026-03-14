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
        @"^\s*define\s+([A-Za-z_][A-Za-z0-9_]*)\s*=\s*Character\(\s*""([^""]+)""(?<args>.*)\)\s*$",
        RegexOptions.Compiled);

    private static readonly Regex ColorRegex = new(
        @"color\s*=\s*""([^""]+)""",
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
        @"^\s*""([^""]+)""\s*:\s*$",
        RegexOptions.Compiled);

    public ProjectStructureSnapshot Read(string? projectPath)
    {
        if (string.IsNullOrWhiteSpace(projectPath) || !Directory.Exists(projectPath))
            return new ProjectStructureSnapshot(Array.Empty<Character>(), Array.Empty<LabelOutlineItem>(), Array.Empty<StructureLinkItem>());

        var characters = new List<Character>();
        var labels = new List<LabelOutlineItem>();
        var links = new List<StructureLinkItem>();

        foreach (var file in Directory.EnumerateFiles(projectPath, "*.rpy", SearchOption.AllDirectories)
                     .OrderBy(static p => p, StringComparer.OrdinalIgnoreCase))
        {
            ParseFile(file, characters, labels, links);
        }

        return new ProjectStructureSnapshot(characters, labels, links);
    }

    private static void ParseFile(
        string filePath,
        List<Character> characters,
        List<LabelOutlineItem> labels,
        List<StructureLinkItem> links)
    {
        var lines = File.ReadAllLines(filePath);
        var fileName = Path.GetFileName(filePath);

        string? currentLabel = null;
        var currentLabelLine = 0;
        var currentStatementCount = 0;

        for (var i = 0; i < lines.Length; i++)
        {
            var lineNumber = i + 1;
            var line = lines[i];
            var trimmed = line.Trim();

            var characterMatch = CharacterRegex.Match(line);
            if (characterMatch.Success)
            {
                var codeName = characterMatch.Groups[1].Value;
                var inGameName = characterMatch.Groups[2].Value;
                var args = characterMatch.Groups["args"].Value;
                var color = ExtractColor(args);
                characters.Add(new Character(codeName, color, inGameName));
            }

            var labelMatch = LabelRegex.Match(line);
            if (labelMatch.Success)
            {
                FlushCurrentLabel(labels, fileName, ref currentLabel, ref currentLabelLine, ref currentStatementCount);
                currentLabel = labelMatch.Groups[1].Value;
                currentLabelLine = lineNumber;
                continue;
            }

            if (currentLabel is null)
                continue;

            if (trimmed.Length > 0 && !trimmed.StartsWith('#'))
                currentStatementCount++;

            var jumpMatch = JumpRegex.Match(line);
            if (jumpMatch.Success)
            {
                links.Add(new StructureLinkItem("jump", currentLabel, jumpMatch.Groups[1].Value, $"jump → {jumpMatch.Groups[1].Value}", fileName, lineNumber));
                continue;
            }

            var callMatch = CallRegex.Match(line);
            if (callMatch.Success)
            {
                links.Add(new StructureLinkItem("call", currentLabel, callMatch.Groups[1].Value, $"call → {callMatch.Groups[1].Value}", fileName, lineNumber));
                continue;
            }

            var menuChoiceMatch = MenuChoiceRegex.Match(line);
            if (menuChoiceMatch.Success)
            {
                var choiceText = menuChoiceMatch.Groups[1].Value;
                var target = TryFindMenuTarget(lines, i + 1) ?? "(inline branch)";
                links.Add(new StructureLinkItem("menu", currentLabel, target, choiceText, fileName, lineNumber));
            }
        }

        FlushCurrentLabel(labels, fileName, ref currentLabel, ref currentLabelLine, ref currentStatementCount);
    }

    private static void FlushCurrentLabel(
        List<LabelOutlineItem> labels,
        string fileName,
        ref string? currentLabel,
        ref int currentLabelLine,
        ref int currentStatementCount)
    {
        if (string.IsNullOrWhiteSpace(currentLabel))
            return;

        labels.Add(new LabelOutlineItem(currentLabel, fileName, currentStatementCount, currentLabelLine));
        currentLabel = null;
        currentLabelLine = 0;
        currentStatementCount = 0;
    }

    private static string ExtractColor(string args)
    {
        var match = ColorRegex.Match(args);
        return match.Success ? match.Groups[1].Value : "#808080";
    }

    private static string? TryFindMenuTarget(IReadOnlyList<string> lines, int startIndex)
    {
        for (var i = startIndex; i < lines.Count; i++)
        {
            var trimmed = lines[i].Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#'))
                continue;

            var jumpMatch = JumpRegex.Match(lines[i]);
            if (jumpMatch.Success)
                return jumpMatch.Groups[1].Value;

            var callMatch = CallRegex.Match(lines[i]);
            if (callMatch.Success)
                return callMatch.Groups[1].Value;

            if (!char.IsWhiteSpace(lines[i][0]))
                break;
        }

        return null;
    }
}
