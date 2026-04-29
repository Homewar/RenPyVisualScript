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
        @"^\s*define\s+([A-Za-z_][A-Za-z0-9_]*)\s*=\s*Character\((?<args>.*)\)\s*$",
        RegexOptions.Compiled);

    private static readonly Regex FirstStringArgumentRegex = new(
        @"^\s*(?:""(?<value_dq>[^""]*)""|'(?<value_sq>[^']*)')",
        RegexOptions.Compiled);

    private static readonly Regex ColorRegex = new(
        @"color\s*=\s*(?:""(?<value_dq>[^""]*)""|'(?<value_sq>[^']*)')",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex LabelRegex = new(
        @"^\s*label\s+([A-Za-z_][A-Za-z0-9_]*)(?:\s*\([^)]*\))?\s*:\s*$",
        RegexOptions.Compiled);

    private static readonly Regex ScreenRegex = new(
        @"^\s*screen\s+([A-Za-z_][A-Za-z0-9_]*)\s*(?:\([^)]*\))?\s*:\s*$",
        RegexOptions.Compiled);

    private static readonly Regex PythonFunctionRegex = new(
        @"^\s*def\s+([A-Za-z_][A-Za-z0-9_]*)\s*\(",
        RegexOptions.Compiled);

    private static readonly Regex CallScreenRegex = new(
        @"^\s*call\s+screen\s+([A-Za-z_][A-Za-z0-9_]*)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex ShowScreenRegex = new(
        @"^\s*show\s+screen\s+([A-Za-z_][A-Za-z0-9_]*)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex ScreenCallbackRegex = new(
        @"\b(?:dragged|dropped|clicked|hovered|unhovered|alternate)\s+([A-Za-z_][A-Za-z0-9_]*)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex JumpRegex = new(
        @"^\s*(?:jump\s+(?<target>[A-Za-z_][A-Za-z0-9_]*)\b|renpy\.jump\(\s*(?:""(?<target_dq>[^""]+)""|'(?<target_sq>[^']+)')\s*\))",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex CallRegex = new(
        @"^\s*(?:call\s+(?<target>[A-Za-z_][A-Za-z0-9_]*)\b|renpy\.call\(\s*(?:""(?<target_dq>[^""]+)""|'(?<target_sq>[^']+)')\s*\))",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex ReturnRegex = new(
        @"^\s*return\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex ScreenActionJumpRegex = new(
        @"\bJump\(\s*(?:""(?<target_dq>[^""]+)""|'(?<target_sq>[^']+)')\s*\)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex ScreenActionCallRegex = new(
        @"\bCall\(\s*(?:""(?<target_dq>[^""]+)""|'(?<target_sq>[^']+)')\s*\)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex MenuChoiceRegex = new(
        @"^\s*(?:""(?<text_dq>[^""]+)""|'(?<text_sq>[^']+)')\s*(?<colon>:)?\s*$",
        RegexOptions.Compiled);

    private static readonly Regex MenuHeaderRegex = new(
        @"^\s*menu(?:\s+(?<name>[A-Za-z_][A-Za-z0-9_]*))?\s*:\s*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex IfHeaderRegex = new(
        @"^\s*if\b.*:\s*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex ElifHeaderRegex = new(
        @"^\s*elif\b.*:\s*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex ElseHeaderRegex = new(
        @"^\s*else\s*:\s*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly HashSet<string> ExcludedFileNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "options.rpy",
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
        var files = EnumerateGameScriptFiles(normalizedProjectPath).ToList();
        var screenTargets = BuildProjectScreenTargets(files);

        foreach (var file in files)
            ParseFile(normalizedProjectPath, file, screenTargets, characters, labels, links);

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

        if (relativePath.Contains("/cache/", StringComparison.OrdinalIgnoreCase)
            || relativePath.Contains("/saves/", StringComparison.OrdinalIgnoreCase))
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
        IReadOnlyDictionary<string, IReadOnlyList<(string kind, string target)>> screenTargets,
        List<Character> characters,
        List<LabelOutlineItem> labels,
        List<StructureLinkItem> links)
    {
        var lines = File.ReadAllLines(filePath);
        var relativePath = Path.GetRelativePath(projectPath, filePath).Replace('\\', '/');
        var fileName = Path.GetFileName(filePath);
        var localPythonFunctionTargets = BuildPythonFunctionTargets(lines);
        var localScreenTargets = BuildScreenTargets(lines, localPythonFunctionTargets);

        for (var i = 0; i < lines.Length; i++)
        {
            var characterMatch = CharacterRegex.Match(lines[i]);
            if (!characterMatch.Success)
                continue;

            var codeName = characterMatch.Groups[1].Value;
            var args = characterMatch.Groups["args"].Value;
            var inGameName = ExtractFirstStringArgument(args);
            if (string.IsNullOrWhiteSpace(inGameName))
                continue;

            var color = ExtractColor(args);
            characters.Add(new Character(codeName, color, inGameName, relativePath, i + 1));
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

            var nextLabelName = GetNextGameLabelName(labelHeaders, i + 1);
            ParseLinksForLabel(
                relativePath,
                labelName,
                startIndex + 2,
                endIndex + 1,
                bodyLines,
                nextLabelName,
                screenTargets,
                localScreenTargets,
                links);
        }
    }

    private static string? GetNextGameLabelName(IReadOnlyList<(string name, int startIndex)> labelHeaders, int startIndex)
    {
        for (var i = startIndex; i < labelHeaders.Count; i++)
        {
            if (IsGameLabel(labelHeaders[i].name))
                return labelHeaders[i].name;
        }

        return null;
    }

    private static Dictionary<string, IReadOnlyList<(string kind, string target)>> BuildPythonFunctionTargets(IReadOnlyList<string> lines)
    {
        var result = new Dictionary<string, IReadOnlyList<(string kind, string target)>>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < lines.Count; i++)
        {
            var functionMatch = PythonFunctionRegex.Match(lines[i]);
            if (!functionMatch.Success)
                continue;

            var functionName = functionMatch.Groups[1].Value;
            var functionIndent = GetIndentWidth(lines[i]);
            var body = CollectIndentedBlock(lines, i + 1, functionIndent);
            var targets = ExtractTargetsFromLines(body);
            if (targets.Count > 0)
                result[functionName] = targets;
        }

        return result;
    }

    private static Dictionary<string, IReadOnlyList<(string kind, string target)>> BuildScreenTargets(
        IReadOnlyList<string> lines,
        IReadOnlyDictionary<string, IReadOnlyList<(string kind, string target)>> pythonFunctionTargets)
    {
        var result = new Dictionary<string, IReadOnlyList<(string kind, string target)>>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < lines.Count; i++)
        {
            var screenMatch = ScreenRegex.Match(lines[i]);
            if (!screenMatch.Success)
                continue;

            var screenName = screenMatch.Groups[1].Value;
            var screenIndent = GetIndentWidth(lines[i]);
            var body = CollectIndentedBlock(lines, i + 1, screenIndent);
            var targets = new List<(string kind, string target)>();

            foreach (var line in body)
            {
                if (IsCommentLine(line))
                    continue;

                targets.AddRange(ExtractTargetsFromLine(line));

                foreach (Match callbackMatch in ScreenCallbackRegex.Matches(line))
                {
                    var functionName = callbackMatch.Groups[1].Value;
                    if (pythonFunctionTargets.TryGetValue(functionName, out var functionTargets))
                        targets.AddRange(functionTargets);
                }
            }

            var distinctTargets = targets
                .Where(target => !string.IsNullOrWhiteSpace(target.target))
                .Distinct()
                .ToList();

            if (distinctTargets.Count > 0)
                result[screenName] = distinctTargets;
        }

        return result;
    }

    private static Dictionary<string, IReadOnlyList<(string kind, string target)>> BuildProjectScreenTargets(IReadOnlyList<string> files)
    {
        var result = new Dictionary<string, List<(string kind, string target)>>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in files)
        {
            var lines = File.ReadAllLines(file);
            var pythonFunctionTargets = BuildPythonFunctionTargets(lines);
            var screenTargets = BuildScreenTargets(lines, pythonFunctionTargets);

            foreach (var (screenName, targets) in screenTargets)
            {
                if (!result.TryGetValue(screenName, out var collectedTargets))
                {
                    collectedTargets = new List<(string kind, string target)>();
                    result[screenName] = collectedTargets;
                }

                foreach (var target in targets)
                {
                    if (!collectedTargets.Contains(target))
                        collectedTargets.Add(target);
                }
            }
        }

        return result.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<(string kind, string target)>)pair.Value,
            StringComparer.OrdinalIgnoreCase);
    }

    private static void ParseLinksForLabel(
        string relativePath,
        string currentLabel,
        int bodyStartLine,
        int bodyEndLine,
        IReadOnlyList<string> bodyLines,
        string? nextLabelName,
        IReadOnlyDictionary<string, IReadOnlyList<(string kind, string target)>> screenTargets,
        IReadOnlyDictionary<string, IReadOnlyList<(string kind, string target)>> localScreenTargets,
        List<StructureLinkItem> links)
    {
        int? currentMenuLine = null;
        var currentMenuIndent = -1;
        string? currentMenuName = null;
        var hasExplicitTransfer = false;
        var hasMenu = false;
        var conditionStack = new Stack<(int groupLine, int indent, string branchKind)>();

        for (var i = 0; i < bodyLines.Count; i++)
        {
            var line = bodyLines[i];
            var lineNumber = bodyStartLine + i;
            var indent = GetIndentWidth(line);
            var trimmed = line.Trim();

            if (trimmed.Length > 0 && !trimmed.StartsWith('#'))
            {
                while (conditionStack.Count > 0 && ShouldExitConditionScope(conditionStack.Peek().indent, indent, trimmed))
                    conditionStack.Pop();
            }

            if (currentMenuLine.HasValue && trimmed.Length > 0 && !trimmed.StartsWith('#') && indent <= currentMenuIndent)
            {
                currentMenuLine = null;
                currentMenuIndent = -1;
                currentMenuName = null;
            }

            if (trimmed.Length == 0 || IsCommentLine(line))
                continue;

            if (IfHeaderRegex.IsMatch(line))
            {
                conditionStack.Push((lineNumber, indent, "if"));
                continue;
            }

            if (ElifHeaderRegex.IsMatch(line))
            {
                var groupLine = ReplaceConditionBranch(conditionStack, indent, lineNumber, "elif");
                conditionStack.Push((groupLine, indent, "elif"));
                continue;
            }

            if (ElseHeaderRegex.IsMatch(line))
            {
                var groupLine = ReplaceConditionBranch(conditionStack, indent, lineNumber, "else");
                conditionStack.Push((groupLine, indent, "else"));
                continue;
            }

            if (MenuHeaderRegex.IsMatch(line))
            {
                var menuMatch = MenuHeaderRegex.Match(line);
                hasMenu = true;
                currentMenuLine = lineNumber;
                currentMenuIndent = indent;
                currentMenuName = menuMatch.Groups["name"].Success ? menuMatch.Groups["name"].Value : null;
                continue;
            }

            var callScreenMatch = CallScreenRegex.Match(line);
            var showScreenMatch = ShowScreenRegex.Match(line);
            if (callScreenMatch.Success || showScreenMatch.Success)
            {
                var screenName = callScreenMatch.Success
                    ? callScreenMatch.Groups[1].Value
                    : showScreenMatch.Groups[1].Value;
                if (screenTargets.TryGetValue(screenName, out var targets))
                {
                    foreach (var targetInfo in targets)
                    {
                        if (!IsGameLabel(targetInfo.target))
                            continue;

                        hasExplicitTransfer = true;
                        var description = $"screen {screenName} → {targetInfo.target}";
                        links.Add(new StructureLinkItem(targetInfo.kind, currentLabel, targetInfo.target, description, relativePath, lineNumber, screenName: screenName));
                    }
                }

                continue;
            }

            var jumpMatch = JumpRegex.Match(line);
            if (jumpMatch.Success)
            {
                // Jumps inside a menu branch are already captured as the menu choice's target — skip them here.
                if (!currentMenuLine.HasValue)
                {
                    var target = ExtractLinkTarget(jumpMatch);
                    if (IsGameLabel(target))
                    {
                        hasExplicitTransfer = true;
                        if (TryGetConditionalContext(conditionStack, indent, out var conditionalContext))
                        {
                            links.Add(new StructureLinkItem("branch", currentLabel, target, $"{conditionalContext.branchKind} → {target}", relativePath, lineNumber, conditionalContext.groupLine));
                        }
                        else
                        {
                            links.Add(new StructureLinkItem("jump", currentLabel, target, $"jump → {target}", relativePath, lineNumber));
                        }
                    }
                }
                continue;
            }

            var callMatch = CallRegex.Match(line);
            if (callMatch.Success)
            {
                // Calls inside a menu branch are already captured as the menu choice's target — skip them here.
                if (!currentMenuLine.HasValue)
                {
                    var target = ExtractLinkTarget(callMatch);
                    if (IsGameLabel(target))
                    {
                        hasExplicitTransfer = true;
                        if (TryGetConditionalContext(conditionStack, indent, out var conditionalContext))
                        {
                            links.Add(new StructureLinkItem("branch", currentLabel, target, $"{conditionalContext.branchKind} → {target}", relativePath, lineNumber, conditionalContext.groupLine));
                        }
                        else
                        {
                            links.Add(new StructureLinkItem("call", currentLabel, target, $"call → {target}", relativePath, lineNumber));
                        }
                    }
                }
                continue;
            }

            if (ReturnRegex.IsMatch(line))
            {
                if (!currentMenuLine.HasValue)
                    hasExplicitTransfer = true;

                continue;
            }

            var menuChoiceMatch = MenuChoiceRegex.Match(line);
            if (menuChoiceMatch.Success)
            {
                var choiceIndent = indent;
                var hasChoiceBlock = HasIndentedBlock(bodyLines, i + 1, choiceIndent);
                if (!menuChoiceMatch.Groups["colon"].Success && !hasChoiceBlock)
                    continue;

                var choiceText = menuChoiceMatch.Groups["text_dq"].Success
                    ? menuChoiceMatch.Groups["text_dq"].Value
                    : menuChoiceMatch.Groups["text_sq"].Value;
                var targets = TryFindMenuTargets(bodyLines, i + 1, choiceIndent);
                if (targets.Count == 0)
                {
                    links.Add(new StructureLinkItem("menu", currentLabel, "(inline branch)", choiceText, relativePath, lineNumber, currentMenuLine ?? lineNumber, menuName: currentMenuName));
                }
                else
                {
                    foreach (var target in targets)
                    {
                        if (IsGameLabel(target))
                            links.Add(new StructureLinkItem("menu", currentLabel, target, choiceText, relativePath, lineNumber, currentMenuLine ?? lineNumber, menuName: currentMenuName));
                    }
                }
            }
        }

        if (!hasExplicitTransfer
            && !hasMenu
            && TryAddSingleLocalScreenFallback(relativePath, currentLabel, bodyEndLine, localScreenTargets, links))
        {
            return;
        }

        if (!hasExplicitTransfer
            && !hasMenu
            && !string.IsNullOrWhiteSpace(nextLabelName)
            && IsGameLabel(nextLabelName))
        {
            links.Add(new StructureLinkItem("fallthrough", currentLabel, nextLabelName!, $"next → {nextLabelName}", relativePath, bodyEndLine));
        }
    }

    private static bool TryAddSingleLocalScreenFallback(
        string relativePath,
        string currentLabel,
        int lineNumber,
        IReadOnlyDictionary<string, IReadOnlyList<(string kind, string target)>> localScreenTargets,
        List<StructureLinkItem> links)
    {
        if (localScreenTargets.Count != 1)
            return false;

        var screen = localScreenTargets.Single();
        var added = false;
        foreach (var targetInfo in screen.Value)
        {
            if (!IsGameLabel(targetInfo.target))
                continue;

            links.Add(new StructureLinkItem(
                targetInfo.kind,
                currentLabel,
                targetInfo.target,
                $"screen {screen.Key} callback в†’ {targetInfo.target}",
                relativePath,
                lineNumber,
                screenName: screen.Key));
            added = true;
        }

        return added;
    }

    private static List<string> TryFindMenuTargets(IReadOnlyList<string> bodyLines, int startIndex, int choiceIndent)
    {
        var targets = new List<string>();

        for (var i = startIndex; i < bodyLines.Count; i++)
        {
            var line = bodyLines[i];
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#'))
                continue;

            var indent = GetIndentWidth(line);
            if (indent <= choiceIndent)
                break;

            var jumpMatch = JumpRegex.Match(line);
            if (jumpMatch.Success)
            {
                var target = ExtractLinkTarget(jumpMatch);
                if (!string.IsNullOrWhiteSpace(target) && !targets.Contains(target, StringComparer.OrdinalIgnoreCase))
                    targets.Add(target);
            }

            var callMatch = CallRegex.Match(line);
            if (callMatch.Success)
            {
                var target = ExtractLinkTarget(callMatch);
                if (!string.IsNullOrWhiteSpace(target) && !targets.Contains(target, StringComparer.OrdinalIgnoreCase))
                    targets.Add(target);
            }
        }

        return targets;
    }

    private static bool HasIndentedBlock(IReadOnlyList<string> bodyLines, int startIndex, int parentIndent)
    {
        for (var i = startIndex; i < bodyLines.Count; i++)
        {
            var line = bodyLines[i];
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#'))
                continue;

            return GetIndentWidth(line) > parentIndent;
        }

        return false;
    }

    private static bool ShouldExitConditionScope(int conditionIndent, int currentIndent, string trimmedLine)
    {
        if (currentIndent > conditionIndent)
            return false;

        if (currentIndent == conditionIndent && (ElifHeaderRegex.IsMatch(trimmedLine) || ElseHeaderRegex.IsMatch(trimmedLine)))
            return false;

        return true;
    }

    private static int ReplaceConditionBranch(Stack<(int groupLine, int indent, string branchKind)> conditionStack, int indent, int lineNumber, string branchKind)
    {
        while (conditionStack.Count > 0)
        {
            var current = conditionStack.Pop();
            if (current.indent == indent)
                return current.groupLine;

            if (current.indent < indent)
            {
                conditionStack.Push(current);
                break;
            }
        }

        return lineNumber;
    }

    private static bool TryGetConditionalContext(
        Stack<(int groupLine, int indent, string branchKind)> conditionStack,
        int currentIndent,
        out (int groupLine, string branchKind) context)
    {
        foreach (var item in conditionStack)
        {
            if (currentIndent > item.indent)
            {
                context = (item.groupLine, item.branchKind);
                return true;
            }
        }

        context = default;
        return false;
    }

    private static List<string> CollectIndentedBlock(IReadOnlyList<string> lines, int startIndex, int parentIndent)
    {
        var block = new List<string>();

        for (var i = startIndex; i < lines.Count; i++)
        {
            var line = lines[i];
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#'))
            {
                block.Add(line);
                continue;
            }

            var indent = GetIndentWidth(line);
            if (indent <= parentIndent)
                break;

            block.Add(line);
        }

        return block;
    }

    private static List<(string kind, string target)> ExtractTargetsFromLines(IEnumerable<string> lines)
    {
        var targets = new List<(string kind, string target)>();
        foreach (var line in lines)
            targets.AddRange(ExtractTargetsFromLine(line));

        return targets;
    }

    private static List<(string kind, string target)> ExtractTargetsFromLine(string line)
    {
        var targets = new List<(string kind, string target)>();
        if (IsCommentLine(line))
            return targets;

        foreach (Match jumpActionMatch in ScreenActionJumpRegex.Matches(line))
        {
            var target = ExtractLinkTarget(jumpActionMatch);
            if (!string.IsNullOrWhiteSpace(target))
                targets.Add(("jump", target));
        }

        foreach (Match callActionMatch in ScreenActionCallRegex.Matches(line))
        {
            var target = ExtractLinkTarget(callActionMatch);
            if (!string.IsNullOrWhiteSpace(target))
                targets.Add(("call", target));
        }

        var jumpMatch = JumpRegex.Match(line);
        if (jumpMatch.Success)
        {
            var target = ExtractLinkTarget(jumpMatch);
            if (!string.IsNullOrWhiteSpace(target))
                targets.Add(("jump", target));
        }

        var callMatch = CallRegex.Match(line);
        if (callMatch.Success)
        {
            var target = ExtractLinkTarget(callMatch);
            if (!string.IsNullOrWhiteSpace(target))
                targets.Add(("call", target));
        }

        return targets;
    }

    private static string ExtractLinkTarget(Match match)
    {
        if (match.Groups["target"].Success)
            return match.Groups["target"].Value;

        if (match.Groups["target_dq"].Success)
            return match.Groups["target_dq"].Value;

        if (match.Groups["target_sq"].Success)
            return match.Groups["target_sq"].Value;

        return "";
    }

    private static bool IsCommentLine(string line)
    {
        return line.TrimStart().StartsWith('#');
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

    private static string ExtractFirstStringArgument(string args)
    {
        var match = FirstStringArgumentRegex.Match(args);
        if (!match.Success)
            return "";

        if (match.Groups["value_dq"].Success)
            return match.Groups["value_dq"].Value;

        if (match.Groups["value_sq"].Success)
            return match.Groups["value_sq"].Value;

        return "";
    }

    private static string ExtractColor(string args)
    {
        var match = ColorRegex.Match(args);
        if (!match.Success)
            return "";

        if (match.Groups["value_dq"].Success)
            return match.Groups["value_dq"].Value;

        if (match.Groups["value_sq"].Success)
            return match.Groups["value_sq"].Value;

        return "";
    }
}
