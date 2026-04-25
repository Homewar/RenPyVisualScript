using System.Threading;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using RenPyVisualScriptMVVM.Modules.Editors.Models;
using RenPyVisualScriptMVVM.Modules.StoryEditor.Models;

namespace RenPyVisualScriptMVVM.Infrastructure.StoryStorage.Interfaces;

public interface IStoryStorageService
{
    Task RebuildProjectIndexAsync(string projectPath, string? projectName = null, CancellationToken cancellationToken = default);
    Task<ProjectStructureSnapshot> ReadProjectStructureAsync(string projectPath, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StoryTextLabelItem>> ReadStoryTextLabelsAsync(string projectPath, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StoryTextFragmentItem>> ReadStoryTextFragmentsAsync(string projectPath, Guid labelId, CancellationToken cancellationToken = default);
    Task UpdateStoryTextFragmentAsync(string projectPath, Guid fragmentId, string plainText, string? projectName = null, CancellationToken cancellationToken = default);
    Task UpdateStoryTextFragmentsAsync(string projectPath, IReadOnlyDictionary<Guid, string> fragmentTexts, string? projectName = null, CancellationToken cancellationToken = default);
    Task UpdateStoryTextFragmentEditsAsync(string projectPath, IReadOnlyList<StoryTextFragmentEdit> fragmentEdits, string? projectName = null, CancellationToken cancellationToken = default);
    Task ApplyStoryTextFragmentChangesAsync(string projectPath, IReadOnlyList<StoryTextFragmentEdit> fragmentEdits, IReadOnlyCollection<Guid> deletedFragmentIds, string? projectName = null, CancellationToken cancellationToken = default);
}
