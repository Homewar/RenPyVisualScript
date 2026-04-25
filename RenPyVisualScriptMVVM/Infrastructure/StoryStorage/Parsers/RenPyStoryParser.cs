using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using RenPyVisualScriptMVVM.Infrastructure.StoryStorage.Interfaces;

namespace RenPyVisualScriptMVVM.Infrastructure.StoryStorage.Parsers;

public sealed class RenPyStoryParser : IRenPyStoryParser
{
    private static readonly Regex LabelRegex = new(@"^\s*label\s+(?<name>[A-Za-z_][A-Za-z0-9_\.]*)\s*:\s*$", RegexOptions.Compiled);
    private static readonly Regex SayRegex = new(@"^\s*(?:(?<speaker>[A-Za-z_][A-Za-z0-9_]*)\s+)?(?<quote>['\""])(?<text>(?:\\.|(?!\k<quote>).)*)\k<quote>", RegexOptions.Compiled);
    private static readonly Regex TokenRegex = new(@"(?<space>\s*)(?<word>\S+)(?<tail>\s*)", RegexOptions.Compiled);

    public ParsedStoryProject ParseProject(string projectPath, string? projectName = null)
    {
        if (string.IsNullOrWhiteSpace(projectPath))
            throw new ArgumentException("Project path is required.", nameof(projectPath));

        if (!Directory.Exists(projectPath))
            throw new DirectoryNotFoundException(projectPath);

        var labels = new List<ParsedLabel>();
        var labelSort = 0;

        foreach (var filePath in Directory.EnumerateFiles(projectPath, "*.rpy", SearchOption.AllDirectories)
                     .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            var relativePath = Path.GetRelativePath(projectPath, filePath).Replace('\\', '/');
            ParseFile(filePath, relativePath, ref labelSort, labels);
        }

        var name = string.IsNullOrWhiteSpace(projectName) ? Path.GetFileName(projectPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)) : projectName.Trim();
        return new ParsedStoryProject(name, Path.GetFullPath(projectPath), labels);
    }

    private static void ParseFile(string filePath, string relativePath, ref int labelSort, ICollection<ParsedLabel> labels)
    {
        var lines = File.ReadAllLines(filePath);
        var headers = new List<(string Name, int Index)>();
        for (var i = 0; i < lines.Length; i++)
        {
            var match = LabelRegex.Match(lines[i]);
            if (match.Success)
                headers.Add((match.Groups["name"].Value, i));
        }

        for (var i = 0; i < headers.Count; i++)
        {
            var (name, startIndex) = headers[i];
            var endIndex = i + 1 < headers.Count ? headers[i + 1].Index - 1 : lines.Length - 1;
            var bodyLines = lines.Skip(startIndex + 1).Take(Math.Max(0, endIndex - startIndex)).ToArray();
            var rawText = string.Join(Environment.NewLine, bodyLines);
            var fragments = ParseFragments(bodyLines, startIndex + 2);
            labels.Add(new ParsedLabel(name, relativePath, startIndex + 1, endIndex + 1, labelSort++, rawText, fragments));
        }
    }

    private static IReadOnlyList<ParsedTextFragment> ParseFragments(IReadOnlyList<string> bodyLines, int sourceLineStart)
    {
        var fragments = new List<ParsedTextFragment>();
        for (var i = 0; i < bodyLines.Count; i++)
        {
            var line = bodyLines[i];
            var trimmed = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith('#'))
                continue;

            var sayMatch = SayRegex.Match(line);
            if (!sayMatch.Success)
                continue;

            var speaker = sayMatch.Groups["speaker"].Success ? sayMatch.Groups["speaker"].Value : null;
            var text = Regex.Unescape(sayMatch.Groups["text"].Value);
            var tagsAndText = ParseWords(text);
            var plainText = string.Concat(tagsAndText.Select(x => x.LeadingTrivia + x.PlainText + x.TrailingTrivia));
            fragments.Add(new ParsedTextFragment(
                fragments.Count,
                sourceLineStart + i,
                "say",
                speaker,
                text,
                plainText,
                tagsAndText));
        }

        return fragments;
    }

    private static IReadOnlyList<ParsedWord> ParseWords(string text)
    {
        var spans = ParseTaggedSpans(text);
        var words = new List<ParsedWord>();

        foreach (var span in spans)
        {
            foreach (Match match in TokenRegex.Matches(span.Text))
            {
                if (!match.Success)
                    continue;

                var word = match.Groups["word"].Value;
                if (string.IsNullOrWhiteSpace(word))
                    continue;

                words.Add(new ParsedWord(
                    words.Count,
                    word,
                    word,
                    match.Groups["space"].Value,
                    match.Groups["tail"].Value,
                    span.OpenedTags
                        .Select((tag, idx) => new ParsedFormatTag(idx, tag.Name, tag.Argument, tag.IsSelfClosing, tag.Raw))
                        .ToArray()));
            }
        }

        return words;
    }

    private static IReadOnlyList<TaggedTextSpan> ParseTaggedSpans(string text)
    {
        var spans = new List<TaggedTextSpan>();
        var buffer = new StringBuilder();
        var activeTags = new List<TagDescriptor>();
        var openedTags = new List<TagDescriptor>();

        void FlushBuffer()
        {
            if (buffer.Length == 0)
                return;

            spans.Add(new TaggedTextSpan(buffer.ToString(), openedTags.Select(x => x.Copy()).ToArray()));
            openedTags.Clear();
            buffer.Clear();
        }

        for (var i = 0; i < text.Length; i++)
        {
            var current = text[i];
            if (current != '{')
            {
                buffer.Append(current);
                continue;
            }

            var end = text.IndexOf('}', i + 1);
            if (end < 0)
            {
                buffer.Append(current);
                continue;
            }

            var rawTag = text.Substring(i, end - i + 1);
            var inner = text.Substring(i + 1, end - i - 1).Trim();
            if (string.IsNullOrWhiteSpace(inner))
            {
                buffer.Append(rawTag);
                i = end;
                continue;
            }

            FlushBuffer();

            if (inner.StartsWith("/", StringComparison.Ordinal))
            {
                var closingName = inner[1..].Trim();
                var index = activeTags.FindLastIndex(x => x.Name.Equals(closingName, StringComparison.OrdinalIgnoreCase));
                if (index >= 0)
                    activeTags.RemoveAt(index);
            }
            else
            {
                var isSelfClosing = inner.Equals("w", StringComparison.OrdinalIgnoreCase)
                    || inner.Equals("nw", StringComparison.OrdinalIgnoreCase)
                    || inner.StartsWith("p", StringComparison.OrdinalIgnoreCase)
                    || inner.StartsWith("fast", StringComparison.OrdinalIgnoreCase)
                    || inner.StartsWith("done", StringComparison.OrdinalIgnoreCase);

                var eqIndex = inner.IndexOf('=');
                var name = eqIndex >= 0 ? inner[..eqIndex].Trim() : inner;
                var arg = eqIndex >= 0 ? inner[(eqIndex + 1)..].Trim() : null;
                var descriptor = new TagDescriptor(name, arg, isSelfClosing, rawTag);

                if (isSelfClosing)
                {
                    spans.Add(new TaggedTextSpan(" ", new[] { descriptor }));
                }
                else
                {
                    activeTags.Add(descriptor);
                    openedTags.Add(descriptor);
                }
            }

            i = end;
        }

        FlushBuffer();
        return spans;
    }

    private sealed record TaggedTextSpan(string Text, IReadOnlyList<TagDescriptor> OpenedTags);

    private sealed record TagDescriptor(string Name, string? Argument, bool IsSelfClosing, string Raw)
    {
        public TagDescriptor Copy() => new(Name, Argument, IsSelfClosing, Raw);
    }
}
