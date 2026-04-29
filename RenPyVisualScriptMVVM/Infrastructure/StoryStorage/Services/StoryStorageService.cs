using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using RenPyVisualScriptMVVM.Core.Models;
using RenPyVisualScriptMVVM.Infrastructure.StoryStorage;
using RenPyVisualScriptMVVM.Infrastructure.StoryStorage.Entities;
using RenPyVisualScriptMVVM.Infrastructure.StoryStorage.Interfaces;
using RenPyVisualScriptMVVM.Infrastructure.StoryStorage.Logging;
using RenPyVisualScriptMVVM.Infrastructure.StoryStorage.Parsers;
using RenPyVisualScriptMVVM.Modules.Editors.Models;
using RenPyVisualScriptMVVM.Modules.Editors.Services;
using RenPyVisualScriptMVVM.Modules.StoryEditor.Models;

namespace RenPyVisualScriptMVVM.Infrastructure.StoryStorage.Services;

public sealed class StoryStorageService : IStoryStorageService
{
    private static readonly Regex SayLineRegex = new(@"^(?<prefix>\s*(?:(?<speaker>[A-Za-z_][A-Za-z0-9_]*)\s+)?)(?<quote>['""])(?<text>(?:\\.|(?!\k<quote>).)*)(\k<quote>)(?<suffix>.*)$", RegexOptions.Compiled);
    private static readonly SemaphoreSlim IndexGate = new(1, 1);
    private readonly IRenPyStoryParser _parser;
    private readonly IDatabaseLogService _databaseLog;
    private readonly RenPyStructureReader _structureReader = new();

    public StoryStorageService(IRenPyStoryParser parser, IDatabaseLogService databaseLog)
    {
        _parser = parser;
        _databaseLog = databaseLog;
    }

    public async Task RebuildProjectIndexAsync(string projectPath, string? projectName = null, CancellationToken cancellationToken = default)
    {
        await IndexGate.WaitAsync(cancellationToken);
        try
        {
            if (string.IsNullOrWhiteSpace(projectPath))
                return;

            _databaseLog.Info(nameof(RebuildProjectIndexAsync), $"Start. ProjectPath='{projectPath}', ProjectName='{projectName ?? ""}'.");
            var parsedProject = _parser.ParseProject(projectPath, projectName);
            var labelCount = parsedProject.Labels.Count;
            var fragmentCount = parsedProject.Labels.Sum(x => x.Fragments.Count);
            var wordCount = parsedProject.Labels.Sum(x => x.Fragments.Sum(fragment => fragment.Words.Count));
            var tagCount = parsedProject.Labels.Sum(x => x.Fragments.Sum(fragment => fragment.Words.Sum(word => word.FormatTags.Count)));
            _databaseLog.Info(
                nameof(RebuildProjectIndexAsync),
                $"Parsed labels={labelCount}, fragments={fragmentCount}, words={wordCount}, tags={tagCount}. Db='{GetDatabasePath(parsedProject.ProjectPath)}'.");

            await using var db = CreateDbContext(parsedProject.ProjectPath);
            await db.Database.EnsureCreatedAsync(cancellationToken);
            await EnsureSchemaAsync(db, cancellationToken);

            var normalizedPath = parsedProject.ProjectPath;
            var projectId = await db.Projects
                .AsNoTracking()
                .Where(x => x.ProjectPath == normalizedPath)
                .Select(x => (Guid?)x.Id)
                .FirstOrDefaultAsync(cancellationToken);

            var indexedAtUtc = DateTimeOffset.UtcNow;
            if (projectId is null)
            {
                var projectEntity = new StoryProjectEntity
                {
                    Id = Guid.NewGuid(),
                    Name = parsedProject.Name,
                    ProjectPath = normalizedPath,
                    ImportedAtUtc = indexedAtUtc,
                    Labels = parsedProject.Labels
                        .Select(label => MapLabel(label, ComputeLabelHash(label), indexedAtUtc))
                        .ToList()
                };

                db.Projects.Add(projectEntity);
                await db.SaveChangesAsync(cancellationToken);
                _databaseLog.Info(nameof(RebuildProjectIndexAsync), "Created new project index.");
                return;
            }

            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            await ReplaceProjectTextIndexAsync(db, projectId.Value, parsedProject.Labels, indexedAtUtc, _databaseLog, cancellationToken);
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE StoryProjects SET Name = {parsedProject.Name}, ImportedAtUtc = {indexedAtUtc} WHERE Id = {projectId.Value}",
                cancellationToken);

            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            _databaseLog.Info(nameof(RebuildProjectIndexAsync), "Completed successfully.");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (StoryStorageException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new StoryStorageException("Не удалось обновить индекс истории в базе данных.", ex);
        }
        finally
        {
            IndexGate.Release();
        }
    }

    public Task<ProjectStructureSnapshot> ReadProjectStructureAsync(string projectPath, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(projectPath))
                return Task.FromResult(EmptySnapshot());

            cancellationToken.ThrowIfCancellationRequested();
            var normalizedPath = Path.GetFullPath(projectPath);
            _databaseLog.Info(nameof(ReadProjectStructureAsync), $"Read structure from files. ProjectPath='{normalizedPath}'.");
            return Task.FromResult(_structureReader.Read(normalizedPath));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new StoryStorageException("Не удалось прочитать индекс истории из базы данных.", ex);
        }
    }

    public async Task<IReadOnlyList<StoryTextLabelItem>> ReadStoryTextLabelsAsync(string projectPath, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(projectPath))
                return Array.Empty<StoryTextLabelItem>();

            var normalizedPath = Path.GetFullPath(projectPath);
            await using var db = CreateDbContext(normalizedPath);
            await db.Database.EnsureCreatedAsync(cancellationToken);
            await EnsureSchemaAsync(db, cancellationToken);

            var project = await db.Projects
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.ProjectPath == normalizedPath, cancellationToken);

            if (project is null)
                return Array.Empty<StoryTextLabelItem>();

            var labels = await db.Labels
                .AsNoTracking()
                .Where(x => x.ProjectId == project.Id)
                .OrderBy(x => x.SortOrder)
                .Select(x => new StoryTextLabelItem(
                    x.Id,
                    x.Name,
                    x.FilePath,
                    x.StartLine,
                    x.Fragments.Count))
                .ToListAsync(cancellationToken);

            _databaseLog.Info(nameof(ReadStoryTextLabelsAsync), $"Read labels={labels.Count}. Db='{GetDatabasePath(normalizedPath)}'.");
            return labels;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new StoryStorageException("Не удалось прочитать label для редактора текста.", ex);
        }
    }

    public async Task<IReadOnlyList<StoryTextFragmentItem>> ReadStoryTextFragmentsAsync(string projectPath, Guid labelId, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(projectPath) || labelId == Guid.Empty)
                return Array.Empty<StoryTextFragmentItem>();

            var normalizedPath = Path.GetFullPath(projectPath);
            await using var db = CreateDbContext(normalizedPath);
            await db.Database.EnsureCreatedAsync(cancellationToken);
            await EnsureSchemaAsync(db, cancellationToken);

            var fragments = await db.Fragments
                .AsNoTracking()
                .Where(x => x.LabelId == labelId)
                .OrderBy(x => x.SortOrder)
                .Select(x => new StoryTextFragmentItem(
                    x.Id,
                    x.LabelId,
                    x.SourceLine,
                    x.SpeakerCode,
                    x.RawText,
                    x.PlainText))
                .ToListAsync(cancellationToken);

            _databaseLog.Info(nameof(ReadStoryTextFragmentsAsync), $"Read fragments={fragments.Count} for LabelId={labelId}.");
            return fragments;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new StoryStorageException("Не удалось прочитать реплики label.", ex);
        }
    }

    public async Task UpdateStoryTextFragmentAsync(string projectPath, Guid fragmentId, string plainText, string? projectName = null, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(projectPath) || fragmentId == Guid.Empty)
                return;

            var normalizedPath = Path.GetFullPath(projectPath);
            _databaseLog.Info(nameof(UpdateStoryTextFragmentAsync), $"Update fragment={fragmentId}.");
            await using var db = CreateDbContext(normalizedPath);
            await db.Database.EnsureCreatedAsync(cancellationToken);
            await EnsureSchemaAsync(db, cancellationToken);

            var fragment = await db.Fragments
                .AsNoTracking()
                .Include(x => x.Label)
                .FirstOrDefaultAsync(x => x.Id == fragmentId, cancellationToken);

            if (fragment?.Label is null)
                throw new InvalidOperationException("Реплика не найдена в индексе.");

            var relativeFilePath = fragment.Label.FilePath.Replace('/', Path.DirectorySeparatorChar);
            var absoluteFilePath = Path.GetFullPath(Path.Combine(normalizedPath, relativeFilePath));
            if (!File.Exists(absoluteFilePath))
                throw new FileNotFoundException("Файл реплики не найден.", absoluteFilePath);

            var lines = await File.ReadAllLinesAsync(absoluteFilePath, cancellationToken);
            var lineIndex = fragment.SourceLine - 1;
            if (lineIndex < 0 || lineIndex >= lines.Length)
                throw new InvalidOperationException($"Строка {fragment.SourceLine} больше не существует в файле.");

            lines[lineIndex] = ReplaceSayLineText(lines[lineIndex], plainText);
            await File.WriteAllLinesAsync(absoluteFilePath, lines, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), cancellationToken);
            await RebuildProjectIndexAsync(normalizedPath, projectName, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (StoryStorageException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new StoryStorageException("Не удалось обновить текст реплики.", ex);
        }
    }

    public async Task UpdateStoryTextFragmentsAsync(string projectPath, IReadOnlyDictionary<Guid, string> fragmentTexts, string? projectName = null, CancellationToken cancellationToken = default)
    {
        var edits = fragmentTexts
            .Select(x => new StoryTextFragmentEdit(x.Key, string.Empty, x.Value, UpdatesSpeaker: false))
            .ToList();
        await UpdateStoryTextFragmentEditsAsync(projectPath, edits, projectName, cancellationToken);
    }

    public async Task UpdateStoryTextFragmentEditsAsync(string projectPath, IReadOnlyList<StoryTextFragmentEdit> fragmentEdits, string? projectName = null, CancellationToken cancellationToken = default)
    {
        await ApplyStoryTextFragmentChangesAsync(
            projectPath,
            fragmentEdits,
            Array.Empty<Guid>(),
            projectName,
            cancellationToken);
    }

    public async Task ApplyStoryTextFragmentChangesAsync(
        string projectPath,
        IReadOnlyList<StoryTextFragmentEdit> fragmentEdits,
        IReadOnlyCollection<Guid> deletedFragmentIds,
        string? projectName = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(projectPath))
                return;

            var normalizedPath = Path.GetFullPath(projectPath);
            _databaseLog.Info(
                nameof(ApplyStoryTextFragmentChangesAsync),
                $"Apply edits={fragmentEdits.Count}, deletes={deletedFragmentIds.Count}.");
            var editMap = fragmentEdits
                .Where(x => x.FragmentId != Guid.Empty)
                .ToDictionary(x => x.FragmentId);
            var deleteIds = deletedFragmentIds
                .Where(x => x != Guid.Empty)
                .ToHashSet();
            deleteIds.ExceptWith(editMap.Keys);

            var fragmentIds = editMap.Keys.Concat(deleteIds).Distinct().ToArray();
            if (fragmentIds.Length == 0)
                return;

            await using var db = CreateDbContext(normalizedPath);
            await db.Database.EnsureCreatedAsync(cancellationToken);
            await EnsureSchemaAsync(db, cancellationToken);

            var fragments = await db.Fragments
                .AsNoTracking()
                .Include(x => x.Label)
                .Where(x => fragmentIds.Contains(x.Id))
                .ToListAsync(cancellationToken);

            if (fragments.Count != fragmentIds.Length || fragments.Any(x => x.Label is null))
                throw new InvalidOperationException("Одна или несколько реплик не найдены в индексе.");

            foreach (var fileGroup in fragments.GroupBy(x => x.Label!.FilePath, StringComparer.OrdinalIgnoreCase))
            {
                var relativeFilePath = fileGroup.Key.Replace('/', Path.DirectorySeparatorChar);
                var absoluteFilePath = Path.GetFullPath(Path.Combine(normalizedPath, relativeFilePath));
                if (!File.Exists(absoluteFilePath))
                    throw new FileNotFoundException("Файл реплики не найден.", absoluteFilePath);

                var lines = await File.ReadAllLinesAsync(absoluteFilePath, cancellationToken);
                foreach (var fragment in fileGroup.OrderByDescending(x => x.SourceLine))
                {
                    var lineIndex = fragment.SourceLine - 1;
                    if (lineIndex < 0 || lineIndex >= lines.Length)
                        throw new InvalidOperationException($"Строка {fragment.SourceLine} больше не существует в файле.");

                    lineIndex = ResolveFragmentLineIndex(lines, fragment, lineIndex);
                    if (deleteIds.Contains(fragment.Id))
                    {
                        lines = lines
                            .Take(lineIndex)
                            .Concat(lines.Skip(lineIndex + 1))
                            .ToArray();
                    }
                    else
                    {
                        var edit = editMap[fragment.Id];
                        var speakerCode = edit.UpdatesSpeaker
                            ? edit.SpeakerCode
                            : fragment.SpeakerCode ?? string.Empty;
                        var replacementLines = BuildSayReplacementLines(lines[lineIndex], speakerCode, edit.Text);
                        lines = lines
                            .Take(lineIndex)
                            .Concat(replacementLines)
                            .Concat(lines.Skip(lineIndex + 1))
                            .ToArray();
                    }
                }

                await File.WriteAllLinesAsync(absoluteFilePath, lines, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), cancellationToken);
            }

            await RebuildProjectIndexAsync(normalizedPath, projectName, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (StoryStorageException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new StoryStorageException("Не удалось обновить текст реплик.", ex);
        }
    }

    public static string GetDatabasePath(string projectPath)
    {
        var baseDir = Path.Combine(projectPath, ".rvsmvm");
        Directory.CreateDirectory(baseDir);
        return Path.Combine(baseDir, "story-index.db");
    }

    private static StoryDbContext CreateDbContext(string projectPath)
    {
        var options = new DbContextOptionsBuilder<StoryDbContext>()
            .UseSqlite($"Data Source={GetDatabasePath(projectPath)}")
            .EnableSensitiveDataLogging()
            .Options;

        return new StoryDbContext(options);
    }

    private static async Task EnsureSchemaAsync(StoryDbContext db, CancellationToken cancellationToken)
    {
        if (!await ColumnExistsAsync(db, "StoryLabels", "ContentHash", cancellationToken))
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE StoryLabels ADD COLUMN ContentHash TEXT NOT NULL DEFAULT '';", cancellationToken);

        if (!await ColumnExistsAsync(db, "StoryLabels", "IndexedAtUtc", cancellationToken))
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE StoryLabels ADD COLUMN IndexedAtUtc TEXT NOT NULL DEFAULT '0001-01-01 00:00:00+00:00';", cancellationToken);

        if (!await ColumnExistsAsync(db, "StoryWords", "LabelId", cancellationToken))
        {
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE StoryWords ADD COLUMN LabelId TEXT NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';", cancellationToken);
            await db.Database.ExecuteSqlRawAsync("""
                UPDATE StoryWords
                SET LabelId = (
                    SELECT StoryTextFragments.LabelId
                    FROM StoryTextFragments
                    WHERE StoryTextFragments.Id = StoryWords.FragmentId
                );
                """, cancellationToken);
        }

        if (!await ColumnExistsAsync(db, "StoryWordFormatTags", "LabelId", cancellationToken))
        {
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE StoryWordFormatTags ADD COLUMN LabelId TEXT NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';", cancellationToken);
            await db.Database.ExecuteSqlRawAsync("""
                UPDATE StoryWordFormatTags
                SET LabelId = (
                    SELECT StoryWords.LabelId
                    FROM StoryWords
                    WHERE StoryWords.Id = StoryWordFormatTags.WordId
                );
                """, cancellationToken);
        }

        await db.Database.ExecuteSqlRawAsync("CREATE INDEX IF NOT EXISTS IX_StoryWords_LabelId ON StoryWords (LabelId);", cancellationToken);
        await db.Database.ExecuteSqlRawAsync("CREATE INDEX IF NOT EXISTS IX_StoryWordFormatTags_LabelId ON StoryWordFormatTags (LabelId);", cancellationToken);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS StoryCharacters (
                Id TEXT NOT NULL CONSTRAINT PK_StoryCharacters PRIMARY KEY,
                ProjectId TEXT NOT NULL,
                Name TEXT NOT NULL,
                Color TEXT NOT NULL,
                InGameName TEXT NOT NULL,
                FilePath TEXT NOT NULL,
                Line INTEGER NOT NULL,
                SortOrder INTEGER NOT NULL,
                CONSTRAINT FK_StoryCharacters_StoryProjects_ProjectId FOREIGN KEY (ProjectId) REFERENCES StoryProjects (Id) ON DELETE CASCADE
            );
            """, cancellationToken);
        if (!await ColumnExistsAsync(db, "StoryCharacters", "Color", cancellationToken))
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE StoryCharacters ADD COLUMN Color TEXT NOT NULL DEFAULT '';", cancellationToken);
        if (!await ColumnExistsAsync(db, "StoryCharacters", "InGameName", cancellationToken))
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE StoryCharacters ADD COLUMN InGameName TEXT NOT NULL DEFAULT '';", cancellationToken);
        await db.Database.ExecuteSqlRawAsync("CREATE INDEX IF NOT EXISTS IX_StoryCharacters_ProjectId_SortOrder ON StoryCharacters (ProjectId, SortOrder);", cancellationToken);
        await db.Database.ExecuteSqlRawAsync("CREATE INDEX IF NOT EXISTS IX_StoryCharacters_ProjectId_Name ON StoryCharacters (ProjectId, Name);", cancellationToken);
    }

    private static async Task ReplaceProjectTextIndexAsync(
        StoryDbContext db,
        Guid projectId,
        IReadOnlyList<ParsedLabel> labels,
        DateTimeOffset indexedAtUtc,
        IDatabaseLogService databaseLog,
        CancellationToken cancellationToken)
    {
        databaseLog.Info(nameof(ReplaceProjectTextIndexAsync), $"Replacing text index for ProjectId={projectId}.");
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM StoryWordFormatTags WHERE LabelId IN (SELECT Id FROM StoryLabels WHERE ProjectId = {projectId})",
            cancellationToken);
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM StoryWords WHERE LabelId IN (SELECT Id FROM StoryLabels WHERE ProjectId = {projectId})",
            cancellationToken);
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM StoryTextFragments WHERE LabelId IN (SELECT Id FROM StoryLabels WHERE ProjectId = {projectId})",
            cancellationToken);
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM StoryLabels WHERE ProjectId = {projectId}",
            cancellationToken);

        db.ChangeTracker.Clear();
        db.Labels.AddRange(labels.Select(label =>
        {
            var entity = MapLabel(label, ComputeLabelHash(label), indexedAtUtc);
            entity.ProjectId = projectId;
            return entity;
        }));
        databaseLog.Info(nameof(ReplaceProjectTextIndexAsync), $"Queued fresh labels={labels.Count}.");
    }

    private static async Task<bool> ColumnExistsAsync(StoryDbContext db, string tableName, string columnName, CancellationToken cancellationToken)
    {
        var connection = db.Database.GetDbConnection();
        var shouldClose = connection.State == System.Data.ConnectionState.Closed;
        if (shouldClose)
            await connection.OpenAsync(cancellationToken);

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = $"PRAGMA table_info({tableName});";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                if (string.Equals(reader.GetString(1), columnName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }
    }

    private static StoryLabelEntity MapLabel(ParsedLabel label, string contentHash, DateTimeOffset indexedAtUtc)
    {
        var labelId = Guid.NewGuid();
        return new StoryLabelEntity
        {
            Id = labelId,
            Name = label.Name,
            FilePath = label.FilePath,
            StartLine = label.StartLine,
            EndLine = label.EndLine,
            SortOrder = label.SortOrder,
            RawText = label.RawText,
            ContentHash = contentHash,
            IndexedAtUtc = indexedAtUtc,
            Fragments = label.Fragments.Select(fragment => MapFragment(fragment, labelId)).ToList()
        };
    }

    private static StoryTextFragmentEntity MapFragment(ParsedTextFragment fragment, Guid labelId)
    {
        return new StoryTextFragmentEntity
        {
            Id = Guid.NewGuid(),
            LabelId = labelId,
            SortOrder = fragment.SortOrder,
            SourceLine = fragment.SourceLine,
            Kind = fragment.Kind,
            SpeakerCode = fragment.SpeakerCode,
            RawText = fragment.RawText,
            PlainText = fragment.PlainText,
            Words = fragment.Words.Select(word => MapWord(word, labelId)).ToList()
        };
    }

    private static StoryWordEntity MapWord(ParsedWord word, Guid labelId)
    {
        return new StoryWordEntity
        {
            Id = Guid.NewGuid(),
            LabelId = labelId,
            SortOrder = word.SortOrder,
            Text = word.Text,
            PlainText = word.PlainText,
            LeadingTrivia = word.LeadingTrivia,
            TrailingTrivia = word.TrailingTrivia,
            FormatTags = word.FormatTags.Select(tag => MapTag(tag, labelId)).ToList()
        };
    }

    private static StoryWordFormatTagEntity MapTag(ParsedFormatTag tag, Guid labelId)
    {
        return new StoryWordFormatTagEntity
        {
            Id = Guid.NewGuid(),
            LabelId = labelId,
            SortOrder = tag.SortOrder,
            TagName = tag.TagName,
            TagArgument = tag.TagArgument,
            IsSelfClosing = tag.IsSelfClosing,
            RawTag = tag.RawTag
        };
    }

    private static StoryCharacterEntity MapCharacter(Character character, int sortOrder)
    {
        return new StoryCharacterEntity
        {
            Id = Guid.NewGuid(),
            SortOrder = sortOrder,
            Name = character.Name,
            Color = character.Color,
            InGameName = character.InGameName,
            FilePath = character.FilePath,
            Line = character.Line
        };
    }

    private static StoryCharacterEntity MapCharacter(Character character, int sortOrder, Guid projectId)
    {
        var entity = MapCharacter(character, sortOrder);
        entity.ProjectId = projectId;
        return entity;
    }

    private static ProjectStructureSnapshot EmptySnapshot()
    {
        return new ProjectStructureSnapshot(Array.Empty<Character>(), Array.Empty<LabelOutlineItem>(), Array.Empty<StructureLinkItem>());
    }

    private static IReadOnlyList<string> SplitLines(string text)
    {
        return text
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n');
    }

    private static string ReplaceSayLineText(string line, string plainText)
    {
        var match = SayLineRegex.Match(line);
        if (!match.Success)
            throw new InvalidOperationException("Строка больше не похожа на Ren'Py dialogue/say statement.");

        var quote = match.Groups["quote"].Value;
        var escaped = EscapeRenPyString(plainText ?? string.Empty, quote[0]);
        return string.Concat(match.Groups["prefix"].Value, quote, escaped, quote, match.Groups["suffix"].Value);
    }

    private static int ResolveFragmentLineIndex(IReadOnlyList<string> lines, StoryTextFragmentEntity fragment, int preferredIndex)
    {
        if (preferredIndex >= 0
            && preferredIndex < lines.Count
            && IsMatchingFragmentLine(lines[preferredIndex], fragment))
        {
            return preferredIndex;
        }

        var start = Math.Max(0, Math.Min(preferredIndex, lines.Count - 1) - 8);
        var end = Math.Min(lines.Count - 1, Math.Max(preferredIndex, 0) + 8);
        for (var i = start; i <= end; i++)
        {
            if (IsMatchingFragmentLine(lines[i], fragment))
                return i;
        }

        var labelStart = Math.Max(0, (fragment.Label?.StartLine ?? 1) - 1);
        var labelEnd = Math.Min(lines.Count - 1, (fragment.Label?.EndLine ?? lines.Count) - 1);
        for (var i = labelStart; i <= labelEnd; i++)
        {
            if (IsMatchingFragmentLine(lines[i], fragment))
                return i;
        }

        if (preferredIndex >= 0 && preferredIndex < lines.Count && SayLineRegex.IsMatch(lines[preferredIndex]))
            return preferredIndex;

        throw new InvalidOperationException($"Line {fragment.SourceLine} is no longer a Ren'Py dialogue/say statement.");
    }

    private static bool IsMatchingFragmentLine(string line, StoryTextFragmentEntity fragment)
    {
        var match = SayLineRegex.Match(line);
        if (!match.Success)
            return false;

        var lineSpeaker = match.Groups["speaker"].Success ? match.Groups["speaker"].Value : string.Empty;
        var fragmentSpeaker = fragment.SpeakerCode ?? string.Empty;
        if (!string.Equals(lineSpeaker, fragmentSpeaker, StringComparison.Ordinal))
            return false;

        var lineText = Regex.Unescape(match.Groups["text"].Value);
        return string.Equals(lineText, fragment.RawText, StringComparison.Ordinal);
    }

    private static IReadOnlyList<string> BuildSayReplacementLines(string line, string speakerCode, string plainText)
    {
        var match = SayLineRegex.Match(line);
        if (!match.Success)
            throw new InvalidOperationException("Line is no longer a Ren'Py dialogue/say statement.");

        var indent = Regex.Match(line, @"^\s*").Value;
        var quote = match.Groups["quote"].Value[0];
        var suffix = match.Groups["suffix"].Value;
        var split = SplitLines(plainText ?? string.Empty);
        if (split.Count == 0)
            split = new[] { string.Empty };

        var result = new List<string>(split.Count);
        for (var i = 0; i < split.Count; i++)
        {
            var lineSuffix = i == 0 ? suffix : string.Empty;
            result.Add(BuildSayLine(indent, speakerCode, quote, split[i], lineSuffix));
        }

        return result;
    }

    private static string BuildSayLine(string indent, string speakerCode, char quote, string plainText, string suffix = "")
    {
        var builder = new StringBuilder(indent);
        if (!string.IsNullOrWhiteSpace(speakerCode))
        {
            builder.Append(speakerCode.Trim());
            builder.Append(' ');
        }

        builder.Append(quote);
        builder.Append(EscapeRenPyString(plainText ?? string.Empty, quote));
        builder.Append(quote);
        builder.Append(suffix);
        return builder.ToString();
    }

    private static string EscapeRenPyString(string text, char quote)
    {
        var escaped = text.Replace("\\", "\\\\", StringComparison.Ordinal);
        return quote == '\''
            ? escaped.Replace("'", "\\'", StringComparison.Ordinal)
            : escaped.Replace("\"", "\\\"", StringComparison.Ordinal);
    }

    private static string ComputeLabelHash(ParsedLabel label)
    {
        var text = string.Concat("story-label-v2\n", label.RawText);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        return Convert.ToHexString(bytes);
    }

}
