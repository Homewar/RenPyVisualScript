using System.Collections.Generic;

namespace RenPyVisualScriptMVVM.Infrastructure.StoryStorage.Parsers;

public sealed record ParsedStoryProject(string Name, string ProjectPath, IReadOnlyList<ParsedLabel> Labels);
public sealed record ParsedLabel(string Name, string FilePath, int StartLine, int EndLine, int SortOrder, string RawText, IReadOnlyList<ParsedTextFragment> Fragments);
public sealed record ParsedTextFragment(int SortOrder, int SourceLine, string Kind, string? SpeakerCode, string RawText, string PlainText, IReadOnlyList<ParsedWord> Words);
public sealed record ParsedWord(int SortOrder, string Text, string PlainText, string LeadingTrivia, string TrailingTrivia, IReadOnlyList<ParsedFormatTag> FormatTags);
public sealed record ParsedFormatTag(int SortOrder, string TagName, string? TagArgument, bool IsSelfClosing, string RawTag);
