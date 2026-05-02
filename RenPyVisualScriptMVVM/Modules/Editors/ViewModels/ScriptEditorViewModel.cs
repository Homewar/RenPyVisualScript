using CommunityToolkit.Mvvm.Input;
using Avalonia.Data.Converters;
using Avalonia.Media;
using RenPyVisualScriptMVVM.Modules.Editors.Models;
using RenPyVisualScriptMVVM.Core.Services.Interfaces;
using RenPyVisualScriptMVVM.Modules.Shell.Services.Interfaces;
using Splat;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using RenPyVisualScriptMVVM.Modules.Shell.ViewModels;
using RenPyVisualScriptMVVM.Modules.Settings.ViewModels;
using RenPyVisualScriptMVVM.Modules.Projects.ViewModels;
using RenPyVisualScriptMVVM.Core.Models;
using RenPyVisualScriptMVVM.Modules.Projects.Models;
using RenPyVisualScriptMVVM.Modules.GraphEditor.ViewModels;
using RenPyVisualScriptMVVM.Modules.DatabaseLog.ViewModels;
using RenPyVisualScriptMVVM.Modules.StoryEditor.ViewModels;
using RenPyVisualScriptMVVM.Modules.Editors.Services;
using RenPyVisualScriptMVVM.Modules.Editors.Services.Interfaces;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using RenPyVisualScriptMVVM.Infrastructure.StoryStorage.Interfaces;
using RenPyVisualScriptMVVM.Infrastructure.StoryStorage.Logging;
using System.Threading.Tasks;

namespace RenPyVisualScriptMVVM.Modules.Editors.ViewModels;

public sealed class ScriptEditorViewModel : BaseViewModel
{
    private static readonly Regex CharacterCodeRegex = new("^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled);
    private static readonly Regex ImageDefinitionPrefixRegex = new(@"^\s*image\s+(?<tag>[A-Za-z_][A-Za-z0-9_]*)\s*=", RegexOptions.Compiled);
    private readonly IProjectContext _ctx;
    private readonly ISettingsService _settings;
    private readonly IDESettings _ide;
    private readonly IWindowService _windows;
    private readonly IEditorNavigationService _editorNavigation;
    private readonly IReadonlyDependencyResolver _loc;
    private readonly IStoryStorageService _storyStorage;
    private readonly IApplicationDialogService _dialogs;
    private readonly RenPyStructureReader _fallbackStructureReader = new();
    private bool _isShowingStoryIndexError;
    private GraphEditorWindowViewModel? _activeGraphViewModel;
    private int _mainEditorSelectedIndex;
    private Character? _selectedCharacter;

    public IRelayCommand SaveCmd { get; }
    public IRelayCommand ShowProjectSetCmd { get; }
    public IRelayCommand ShowAppSettingsCmd { get; }
    public IRelayCommand ShowIdeSettingsCmd { get; }
    public IRelayCommand RunProjectCmd { get; }
    public IRelayCommand RunFromHereCmd { get; }
    public IRelayCommand OpenGraphCmd { get; }
    public IRelayCommand OpenStoryTextEditorCmd { get; }
    public IRelayCommand OpenDatabaseLogCmd { get; }
    public IRelayCommand RefreshStructureCmd { get; }

    public ObservableCollection<TabItemModel> Tabs { get; } = new();
    public ObservableCollection<Character> CharacterList { get; } = new();
    public ObservableCollection<LabelOutlineItem> LabelList { get; } = new();
    public ObservableCollection<StructureLinkItem> StructureLinks { get; } = new();
    public ObservableCollection<TransitionPanelItem> TransitionItems { get; } = new();
    public ObservableCollection<ResourceFileItem> ImageResources { get; } = new();
    public ObservableCollection<ResourceFileItem> AudioResources { get; } = new();
    public ObservableCollection<ResourceFileItem> VideoResources { get; } = new();
    public ObservableCollection<ResourceFileItem> FontResources { get; } = new();

    public StoryTextEditorWindowViewModel StoryTextEditorVm { get; }

    public Character? SelectedCharacter
    {
        get => _selectedCharacter;
        set
        {
            if (!SetProperty(ref _selectedCharacter, value))
                return;

            StoryTextEditorVm.ActiveSpeakerCode = value?.Name ?? "Narrator";
        }
    }

    public int MainEditorSelectedIndex
    {
        get => _mainEditorSelectedIndex;
        set
        {
            if (SetProperty(ref _mainEditorSelectedIndex, value))
            {
                OnPropertyChanged(nameof(IsScriptsTabVisible));
                OnPropertyChanged(nameof(IsStoryTabVisible));
            }
        }
    }

    public bool IsScriptsTabVisible => MainEditorSelectedIndex == 0;
    public bool IsStoryTabVisible => MainEditorSelectedIndex == 1;

    private string _structureSummary = "Проект ещё не разобран";
    public string StructureSummary
    {
        get => _structureSummary;
        private set => SetProperty(ref _structureSummary, value);
    }

    private TabItemModel? _selectedTab;
    public TabItemModel? SelectedTab
    {
        get => _selectedTab;
        set
        {
            if (_selectedTab == value)
                return;

            if (_selectedTab is not null)
                _selectedTab.PropertyChanged -= OnSelectedTabPropertyChanged;

            if (SetProperty(ref _selectedTab, value))
            {
                if (_selectedTab is not null)
                    _selectedTab.PropertyChanged += OnSelectedTabPropertyChanged;

                OnPropertyChanged(nameof(RunButtonText));
                OnPropertyChanged(nameof(StartPointText));
            }
        }
    }

    public string RunButtonText => SelectedTab?.ActiveBreakpointLine is int line && line > 0
        ? $"Run from line {line}"
        : "Run";

    public string StartPointText => SelectedTab?.ActiveBreakpointLine is int line && line > 0
        ? $"Start point: line {line}"
        : "Start point: not set";

    public FileTreeViewModel FileTreeVm { get; }
    public ObservableCollection<FileNode> FileTreeNodes => FileTreeVm.Nodes;

    private FileNode? _selectedFileNode;
    public FileNode? SelectedFileNode
    {
        get => _selectedFileNode;
        set
        {
            if (SetProperty(ref _selectedFileNode, value) && value is not null)
                OpenFileInTab(value);
        }
    }

    public string? ProjectName
    {
        get => _ctx.ProjectName;
        set => _ctx.ProjectName = value;
    }

    public string? ProjectPath => _ctx.ProjectPath;

    public IReadOnlyList<ResourceFileItem> AvailableCharacterImages => ImageResources;

    public bool HasUnsavedChanges => Tabs.Any(tab => tab.IsModified);

    public int UnsavedTabCount => Tabs.Count(tab => tab.IsModified);

    public ScriptEditorViewModel(
        IProjectContext ctx,
        ISettingsService settings,
        IDESettings ide,
        IWindowService windows,
        IEditorNavigationService editorNavigation,
        IStoryStorageService storyStorage,
        IApplicationDialogService dialogs,
        IDatabaseLogService databaseLog,
        IReadonlyDependencyResolver? loc = null)
    {
        _ctx = ctx;
        _settings = settings;
        _ide = ide;
        _windows = windows;
        _editorNavigation = editorNavigation;
        _storyStorage = storyStorage;
        _dialogs = dialogs;
        _loc = loc ?? Locator.Current;

        FileTreeVm = new FileTreeViewModel(_ctx, _ide.ShowSystemResources);
        StoryTextEditorVm = new StoryTextEditorWindowViewModel(_ctx, _storyStorage, _dialogs);
        StoryTextEditorVm.SourceFilesChanged += OnStoryTextSourceFilesChanged;

        SaveCmd = new RelayCommand(SaveProject);
        ShowProjectSetCmd = new RelayCommand(OpenProjectSettings);
        ShowAppSettingsCmd = new RelayCommand(OpenAppSettings);
        ShowIdeSettingsCmd = new RelayCommand(OpenIdeSettings);
        RunProjectCmd = new RelayCommand(RunProject);
        RunFromHereCmd = new RelayCommand(RunFromHere);
        OpenGraphCmd = new RelayCommand(OpenGraphWindow);
        OpenStoryTextEditorCmd = new RelayCommand(OpenStoryTextEditorWindow);
        OpenDatabaseLogCmd = new RelayCommand(OpenDatabaseLogWindow);
        RefreshStructureCmd = new RelayCommand(() => RefreshStructure());

        _ctx.PropertyChanged += OnProjectContextChanged;
        _ide.PropertyChanged += OnIdeSettingsChanged;
        _editorNavigation.RegisterHandler(NavigateToFile);
        RefreshStructure();

        Debug.WriteLine("ScriptEditorViewModel initialized");
    }

    private void OnProjectContextChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(IProjectContext.ProjectPath))
        {
            RefreshStructure();
        }
    }

    private void OnIdeSettingsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(IDESettings.ShowSystemResources))
        {
            RefreshStructure();
        }
    }

    private void OnSelectedTabPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(TabItemModel.ActiveBreakpointLine) or nameof(TabItemModel.BreakpointsVersion))
        {
            OnPropertyChanged(nameof(RunButtonText));
            OnPropertyChanged(nameof(StartPointText));
        }
    }

    private void RefreshStructure(bool refreshFileTree = true, bool rebuildStoryIndex = true, string? refreshReason = null)
    {
        _ = RefreshStructureAsync(refreshFileTree, rebuildStoryIndex, refreshReason);
    }

    private async Task RefreshStructureAsync(bool refreshFileTree = true, bool rebuildStoryIndex = true, string? refreshReason = null)
    {
        if (refreshFileTree)
        {
            FileTreeVm.ShowSystemResources = _ide.ShowSystemResources;
            FileTreeVm.Refresh();
        }

        ImageResources.Clear();
        AudioResources.Clear();
        VideoResources.Clear();
        FontResources.Clear();

        var snapshot = new ProjectStructureSnapshot(
            Array.Empty<Character>(),
            Array.Empty<LabelOutlineItem>(),
            Array.Empty<StructureLinkItem>());

        if (!string.IsNullOrWhiteSpace(_ctx.ProjectPath))
        {
            if (rebuildStoryIndex)
            {
                try
                {
                    await _storyStorage.RebuildProjectIndexAsync(_ctx.ProjectPath, _ctx.ProjectName);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Story index refresh error: {ex}");
                    await ShowStoryIndexErrorAsync(ex, refreshReason);
                }
            }

            try
            {
                snapshot = await _storyStorage.ReadProjectStructureAsync(_ctx.ProjectPath);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Project structure refresh error: {ex}");
                snapshot = _fallbackStructureReader.Read(_ctx.ProjectPath);
            }
        }

        if (snapshot.Labels.Count == 0 && !string.IsNullOrWhiteSpace(_ctx.ProjectPath))
        {
            var fallbackSnapshot = _fallbackStructureReader.Read(_ctx.ProjectPath);
            if (fallbackSnapshot.Labels.Count > 0)
                snapshot = fallbackSnapshot;
        }

        CharacterList.Clear();
        LabelList.Clear();
        StructureLinks.Clear();
        TransitionItems.Clear();

        var selectedCharacterName = SelectedCharacter?.Name;
        foreach (var character in snapshot.Characters)
            CharacterList.Add(character);

        SelectedCharacter = string.IsNullOrWhiteSpace(selectedCharacterName)
            ? null
            : CharacterList.FirstOrDefault(character =>
                string.Equals(character.Name, selectedCharacterName, StringComparison.OrdinalIgnoreCase));

        foreach (var label in snapshot.Labels)
            LabelList.Add(label);

        foreach (var link in snapshot.Links)
            StructureLinks.Add(link);

        foreach (var item in BuildTransitionItems(snapshot.Links))
            TransitionItems.Add(item);

        LoadResourceFiles(_ctx.ProjectPath);

        var projectName = string.IsNullOrWhiteSpace(ProjectName) ? "Проект" : ProjectName;
        StructureSummary = $"{projectName}: {CharacterList.Count} персонажей, {LabelList.Count} label, {StructureLinks.Count} связей, {ImageResources.Count} изображений, {AudioResources.Count} аудио, {VideoResources.Count} видео, {FontResources.Count} шрифтов";
    }

    private async Task RebuildStoryIndexAsync()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_ctx.ProjectPath))
                return;

            await _storyStorage.RebuildProjectIndexAsync(_ctx.ProjectPath, _ctx.ProjectName);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Story index rebuild error: {ex}");
            await ShowStoryIndexErrorAsync(ex);
        }
    }

    private async Task ShowStoryIndexErrorAsync(Exception ex, string? context = null)
    {
        if (_isShowingStoryIndexError)
            return;

        try
        {
            _isShowingStoryIndexError = true;
            var operation = string.IsNullOrWhiteSpace(context)
                ? "Операция: обновление текстового индекса label/content в SQLite."
                : $"Операция: {context}";

            await _dialogs.ShowErrorAsync(
                "Ошибка обновления индекса сюжета",
                string.Join(
                    Environment.NewLine,
                    operation,
                    "Граф и файлы проекта уже могут быть сохранены корректно; эта ошибка относится только к обновлению данных для Story Text Editor.",
                    "Правый блок редактора будет перечитан напрямую из .rpy, но список label/реплик в Story Text Editor может остаться устаревшим.",
                    $"Причина: {ex.GetType().Name}: {ex.Message}"),
                ex);
        }
        finally
        {
            _isShowingStoryIndexError = false;
        }
    }

    private void LoadResourceFiles(string? projectPath)
    {
        if (string.IsNullOrWhiteSpace(projectPath) || !Directory.Exists(projectPath))
            return;

        try
        {
            foreach (var file in Directory.EnumerateFiles(projectPath, "*", SearchOption.AllDirectories)
                         .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                if (!_ide.ShowSystemResources && IsSystemResourcePath(file, projectPath))
                    continue;

                var ext = Path.GetExtension(file);
                if (IsImageExtension(ext))
                {
                    ImageResources.Add(new ResourceFileItem(file, projectPath));
                    continue;
                }

                if (IsAudioExtension(ext))
                {
                    AudioResources.Add(new ResourceFileItem(file, projectPath));
                    continue;
                }

                if (IsVideoExtension(ext))
                {
                    VideoResources.Add(new ResourceFileItem(file, projectPath));
                    continue;
                }

                if (IsFontExtension(ext))
                    FontResources.Add(new ResourceFileItem(file, projectPath));
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"LoadResourceFiles error: {ex}");
        }
    }

    private static bool IsSystemResourcePath(string filePath, string projectPath)
    {
        var relativePath = Path.GetRelativePath(projectPath, filePath);
        var segments = relativePath
            .Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);
        var normalizedPath = relativePath.Replace('\\', '/');
        var fileName = Path.GetFileName(filePath);

        if (segments.Any(segment =>
            segment.Equals("renpy", StringComparison.OrdinalIgnoreCase)
            || segment.Equals("lib", StringComparison.OrdinalIgnoreCase)
            || segment.Equals("python-packages", StringComparison.OrdinalIgnoreCase)
            || segment.Equals(".git", StringComparison.OrdinalIgnoreCase)
            || segment.Equals(".vs", StringComparison.OrdinalIgnoreCase)
            || segment.Equals("obj", StringComparison.OrdinalIgnoreCase)
            || segment.Equals("bin", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        if (normalizedPath.Contains("/game/gui/", StringComparison.OrdinalIgnoreCase)
            || normalizedPath.Contains("/gui/", StringComparison.OrdinalIgnoreCase)
            || normalizedPath.Contains("/game/images/gui/", StringComparison.OrdinalIgnoreCase)
            || normalizedPath.Contains("/images/gui/", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return fileName.Equals("gui.rpy", StringComparison.OrdinalIgnoreCase)
            || fileName.Equals("screens.rpy", StringComparison.OrdinalIgnoreCase)
            || fileName.Equals("options.rpy", StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<TransitionPanelItem> BuildTransitionItems(IReadOnlyList<StructureLinkItem> links)
    {
        var orderedLinks = links
            .OrderBy(link => link.FileName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(link => link.Line)
            .ToList();

        for (var i = 0; i < orderedLinks.Count; i++)
        {
            var current = orderedLinks[i];
            var isGroupedKind = string.Equals(current.Kind, "menu", StringComparison.OrdinalIgnoreCase)
                || string.Equals(current.Kind, "branch", StringComparison.OrdinalIgnoreCase);

            if (!isGroupedKind)
            {
                yield return new TransitionPanelItem(current);
                continue;
            }

            var menuChoices = new List<StructureLinkItem> { current };
            while (i + 1 < orderedLinks.Count
                   && string.Equals(orderedLinks[i + 1].Kind, current.Kind, StringComparison.OrdinalIgnoreCase)
                   && string.Equals(orderedLinks[i + 1].Source, current.Source, StringComparison.OrdinalIgnoreCase)
                   && string.Equals(orderedLinks[i + 1].FileName, current.FileName, StringComparison.OrdinalIgnoreCase)
                   && orderedLinks[i + 1].GroupLine == current.GroupLine)
            {
                i++;
                menuChoices.Add(orderedLinks[i]);
            }

            yield return new TransitionPanelItem(current.Source, current.FileName, current.GroupLine, menuChoices);
        }
    }

    private void OpenFileInTab(FileNode node)
    {
        OpenFileInTab(node.FullPath, node.Name);
    }

    public void CreateCharacter(CharacterCreationRequest request)
    {
        if (string.IsNullOrWhiteSpace(ProjectPath))
            throw new InvalidOperationException("Project path is not set.");

        if (request is null)
            throw new InvalidOperationException("Character data is missing.");

        var codeName = request.CodeName.Trim();
        var displayName = request.DisplayName.Trim();
        var color = request.Color.Trim();
        var imageTag = request.ImageTag?.Trim();
        var imageSourcePath = request.ImageSourcePath?.Trim();

        if (!CharacterCodeRegex.IsMatch(codeName))
            throw new InvalidOperationException("Code name must start with a letter or '_' and contain only letters, digits, and '_'.");

        if (string.IsNullOrWhiteSpace(displayName))
            throw new InvalidOperationException("Display name cannot be empty.");

        if (CharacterList.Any(c => string.Equals(c.Name, codeName, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"Character '{codeName}' already exists.");

        if (!string.IsNullOrWhiteSpace(color) && !color.StartsWith("#", StringComparison.Ordinal))
            throw new InvalidOperationException("Color must be empty or in hex format like #ffffff.");

        string? resolvedImageRelativePath = null;
        if (!string.IsNullOrWhiteSpace(imageSourcePath))
        {
            var imported = ImportCharacterImage(ProjectPath, codeName, imageSourcePath);
            imageSourcePath = imported.fullPath;
            resolvedImageRelativePath = imported.relativePath;
            imageTag ??= codeName;
        }

        var targetFile = GetCharacterDefinitionsFilePath(ProjectPath);
        Directory.CreateDirectory(Path.GetDirectoryName(targetFile)!);

        var existingLines = File.Exists(targetFile) ? File.ReadAllLines(targetFile) : Array.Empty<string>();
        var lineNumber = existingLines.Length + 1;
        if (existingLines.Length > 0 && !string.IsNullOrWhiteSpace(existingLines[^1]))
            lineNumber++;

        var builder = new StringBuilder();
        if (existingLines.Length > 0 && !string.IsNullOrWhiteSpace(existingLines[^1]))
            builder.AppendLine();

        if (!string.IsNullOrWhiteSpace(resolvedImageRelativePath)
            && !string.IsNullOrWhiteSpace(imageTag)
            && !HasImageDefinition(existingLines, imageTag))
            builder.AppendLine(BuildImageDefinition(imageTag, resolvedImageRelativePath));

        builder.AppendLine(BuildCharacterDefinition(codeName, displayName, color, imageTag));
        File.AppendAllText(targetFile, builder.ToString(), Encoding.UTF8);

        RefreshStructure();
        ReloadOpenTab(targetFile, lineNumber);
        NavigateToFile(targetFile, lineNumber);
    }

    public void NavigateToFile(string filePath, int? line = null)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return;

        if (!Path.IsPathRooted(filePath) && !string.IsNullOrWhiteSpace(ProjectPath))
            filePath = Path.GetFullPath(Path.Combine(ProjectPath, filePath));

        OpenFileInTab(filePath, Path.GetFileName(filePath), line);
    }

    public string CreateFile(string fileName, FileNode? selectedNode)
    {
        if (string.IsNullOrWhiteSpace(ProjectPath))
            throw new InvalidOperationException("Project path is not set.");

        var trimmedName = fileName.Trim();
        if (string.IsNullOrWhiteSpace(trimmedName))
            throw new InvalidOperationException("File name cannot be empty.");

        if (trimmedName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            throw new InvalidOperationException("File name contains invalid characters.");

        var parentDirectory = ResolveCreationDirectory(selectedNode);
        var targetPath = Path.Combine(parentDirectory, trimmedName);
        if (Directory.Exists(targetPath) || File.Exists(targetPath))
            throw new InvalidOperationException($"'{trimmedName}' already exists.");

        Directory.CreateDirectory(parentDirectory);
        using (File.Create(targetPath))
        {
        }

        var createdNode = FileTreeVm.AddPath(targetPath);
        RefreshStructure(refreshFileTree: false);
        SelectedFileNode = createdNode;
        NavigateToFile(targetPath);
        return targetPath;
    }

    public string CreateFolder(string folderName, FileNode? selectedNode)
    {
        if (string.IsNullOrWhiteSpace(ProjectPath))
            throw new InvalidOperationException("Project path is not set.");

        var trimmedName = folderName.Trim();
        if (string.IsNullOrWhiteSpace(trimmedName))
            throw new InvalidOperationException("Folder name cannot be empty.");

        if (trimmedName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            throw new InvalidOperationException("Folder name contains invalid characters.");

        var parentDirectory = ResolveCreationDirectory(selectedNode);
        var targetPath = Path.Combine(parentDirectory, trimmedName);
        if (Directory.Exists(targetPath) || File.Exists(targetPath))
            throw new InvalidOperationException($"'{trimmedName}' already exists.");

        Directory.CreateDirectory(targetPath);
        var createdNode = FileTreeVm.AddPath(targetPath);
        RefreshStructure(refreshFileTree: false);
        SelectedFileNode = createdNode;
        return targetPath;
    }

    public void DeleteFileSystemEntry(FileNode? selectedNode)
    {
        if (selectedNode is null)
            throw new InvalidOperationException("Nothing is selected.");

        if (selectedNode.IsRoot)
            throw new InvalidOperationException("Root project folder cannot be deleted.");

        if (selectedNode.IsDirectory)
        {
            if (Directory.Exists(selectedNode.FullPath))
                Directory.Delete(selectedNode.FullPath, recursive: true);
        }
        else
        {
            if (File.Exists(selectedNode.FullPath))
                File.Delete(selectedNode.FullPath);
        }

        var openTab = Tabs.FirstOrDefault(t =>
            string.Equals(Path.GetFullPath(t.FilePath), Path.GetFullPath(selectedNode.FullPath), StringComparison.OrdinalIgnoreCase));

        if (openTab is not null)
            Tabs.Remove(openTab);

        FileTreeVm.RemovePath(selectedNode.FullPath);
        SelectedFileNode = selectedNode.Parent;
        RefreshStructure(refreshFileTree: false);
    }

    public string RenameFileSystemEntry(FileNode? selectedNode, string newName)
    {
        if (selectedNode is null)
            throw new InvalidOperationException("Nothing is selected.");

        if (selectedNode.IsRoot)
            throw new InvalidOperationException("Root project folder cannot be renamed.");

        var trimmedName = newName.Trim();
        if (string.IsNullOrWhiteSpace(trimmedName))
            throw new InvalidOperationException("Name cannot be empty.");

        if (trimmedName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            throw new InvalidOperationException("Name contains invalid characters.");

        var currentPath = Path.GetFullPath(selectedNode.FullPath);
        var parentDirectory = Path.GetDirectoryName(currentPath);
        if (string.IsNullOrWhiteSpace(parentDirectory))
            throw new InvalidOperationException("Parent directory was not found.");

        var targetPath = Path.Combine(parentDirectory, trimmedName);
        if (string.Equals(currentPath, targetPath, StringComparison.OrdinalIgnoreCase))
            return currentPath;

        if (File.Exists(targetPath) || Directory.Exists(targetPath))
            throw new InvalidOperationException($"'{trimmedName}' already exists.");

        if (selectedNode.IsDirectory)
            Directory.Move(currentPath, targetPath);
        else
            File.Move(currentPath, targetPath);

        UpdateOpenTabsAfterRename(currentPath, targetPath, selectedNode.IsDirectory);
        var renamedNode = FileTreeVm.RenamePath(currentPath, targetPath);
        RefreshStructure(refreshFileTree: false);
        SelectedFileNode = renamedNode;
        NavigateToFile(targetPath);
        return targetPath;
    }

    public bool CanMoveFileSystemEntry(string sourcePath, FileNode? targetNode)
    {
        if (targetNode is null || string.IsNullOrWhiteSpace(sourcePath))
            return false;

        var sourceNode = FileTreeVm.FindNode(sourcePath);
        if (sourceNode is null || sourceNode.IsRoot)
            return false;

        var sourceFullPath = Path.GetFullPath(sourceNode.FullPath);
        var targetDirectory = ResolveMoveTargetDirectory(targetNode);
        if (string.IsNullOrWhiteSpace(targetDirectory))
            return false;

        var targetFullDirectory = Path.GetFullPath(targetDirectory);
        var sourceParent = Path.GetDirectoryName(sourceFullPath);
        if (string.Equals(sourceParent, targetFullDirectory, StringComparison.OrdinalIgnoreCase))
            return false;

        if (sourceNode.IsDirectory && IsSameOrDescendantPath(sourceFullPath, targetFullDirectory))
            return false;

        var targetPath = Path.Combine(targetFullDirectory, Path.GetFileName(sourceFullPath));
        return !File.Exists(targetPath) && !Directory.Exists(targetPath);
    }

    public string MoveFileSystemEntry(FileNode? sourceNode, FileNode? targetNode)
    {
        if (sourceNode is null || targetNode is null)
            throw new InvalidOperationException("Source or target is missing.");

        if (sourceNode.IsRoot)
            throw new InvalidOperationException("Root project folder cannot be moved.");

        var sourcePath = Path.GetFullPath(sourceNode.FullPath);
        var targetDirectory = ResolveMoveTargetDirectory(targetNode);
        if (string.IsNullOrWhiteSpace(targetDirectory))
            throw new InvalidOperationException("Target directory was not found.");

        var targetDirectoryPath = Path.GetFullPath(targetDirectory);
        var sourceParent = Path.GetDirectoryName(sourcePath);
        if (string.Equals(sourceParent, targetDirectoryPath, StringComparison.OrdinalIgnoreCase))
            return sourcePath;

        if (sourceNode.IsDirectory && IsSameOrDescendantPath(sourcePath, targetDirectoryPath))
            throw new InvalidOperationException("A folder cannot be moved into itself or one of its children.");

        var targetPath = Path.Combine(targetDirectoryPath, Path.GetFileName(sourcePath));
        if (File.Exists(targetPath) || Directory.Exists(targetPath))
            throw new InvalidOperationException($"'{Path.GetFileName(sourcePath)}' already exists in target folder.");

        if (sourceNode.IsDirectory)
            Directory.Move(sourcePath, targetPath);
        else
            File.Move(sourcePath, targetPath);

        UpdateOpenTabsAfterRename(sourcePath, targetPath, sourceNode.IsDirectory);
        FileTreeVm.RemovePath(sourcePath);
        var movedNode = FileTreeVm.AddPath(targetPath);
        RefreshStructure(refreshFileTree: false);
        SelectedFileNode = movedNode;
        NavigateToFile(targetPath);
        return targetPath;
    }

    private void OpenFileInTab(string filePath, string header, int? line = null)
    {
        try
        {
            if (!File.Exists(filePath))
                return;

            var ext = Path.GetExtension(filePath);
            if (IsImageExtension(ext))
                return;

            var existing = Tabs.FirstOrDefault(t => t.FilePath == filePath);
            if (existing != null)
            {
                MainEditorSelectedIndex = 0;
                existing.RequestNavigation(line);
                SelectedTab = existing;
                Debug.WriteLine($"Activated existing tab: {existing.FilePath}");
                return;
            }

            var tab = new TabItemModel(
                header,
                filePath,
                closeAction: t => Tabs.Remove(t),
                fileSavedAction: OnTabFileSaved);

            tab.RequestNavigation(line);
            Tabs.Add(tab);
            SelectedTab = tab;
            MainEditorSelectedIndex = 0;
            Debug.WriteLine($"Opened new tab for: {filePath}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"OpenFileInTab error: {ex}");
        }
    }

    private string ResolveCreationDirectory(FileNode? selectedNode)
    {
        if (selectedNode is null)
            return ProjectPath!;

        if (selectedNode.IsDirectory)
            return selectedNode.FullPath;

        var directory = Path.GetDirectoryName(selectedNode.FullPath);
        return string.IsNullOrWhiteSpace(directory) ? ProjectPath! : directory;
    }

    private string? ResolveMoveTargetDirectory(FileNode targetNode)
    {
        if (targetNode.IsDirectory)
            return targetNode.FullPath;

        return Path.GetDirectoryName(targetNode.FullPath);
    }

    private void ReloadOpenTab(string filePath, int? line = null)
    {
        var normalizedPath = Path.GetFullPath(filePath);
        var existing = Tabs.FirstOrDefault(t =>
            string.Equals(Path.GetFullPath(t.FilePath), normalizedPath, StringComparison.OrdinalIgnoreCase));

        existing?.RequestReload(line);
    }

    private void UpdateOpenTabsAfterRename(string oldPath, string newPath, bool isDirectory)
    {
        var normalizedOldPath = Path.GetFullPath(oldPath);
        var normalizedNewPath = Path.GetFullPath(newPath);

        foreach (var tab in Tabs.ToList())
        {
            var normalizedTabPath = Path.GetFullPath(tab.FilePath);
            if (isDirectory)
            {
                var relativePath = TryGetRelativeDescendantPath(normalizedOldPath, normalizedTabPath);
                if (relativePath is null)
                    continue;

                var movedPath = Path.Combine(normalizedNewPath, relativePath);
                tab.UpdateFileIdentity(Path.GetFileName(movedPath), movedPath);
                tab.RequestReload();
                continue;
            }

            if (!string.Equals(normalizedTabPath, normalizedOldPath, StringComparison.OrdinalIgnoreCase))
                continue;

            tab.UpdateFileIdentity(Path.GetFileName(normalizedNewPath), normalizedNewPath);
            tab.RequestReload();
        }
    }

    private static string? TryGetRelativeDescendantPath(string parentPath, string childPath)
    {
        var normalizedParent = parentPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var normalizedChild = Path.GetFullPath(childPath);

        if (!normalizedChild.StartsWith(normalizedParent, StringComparison.OrdinalIgnoreCase))
            return null;

        if (normalizedChild.Length == normalizedParent.Length)
            return string.Empty;

        var nextChar = normalizedChild[normalizedParent.Length];
        if (nextChar != Path.DirectorySeparatorChar && nextChar != Path.AltDirectorySeparatorChar)
            return null;

        return normalizedChild[(normalizedParent.Length + 1)..];
    }

    private static bool IsSameOrDescendantPath(string parentPath, string childPath)
    {
        var normalizedParent = Path.GetFullPath(parentPath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var normalizedChild = Path.GetFullPath(childPath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        return string.Equals(normalizedParent, normalizedChild, StringComparison.OrdinalIgnoreCase)
            || normalizedChild.StartsWith(
                normalizedParent + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase)
            || normalizedChild.StartsWith(
                normalizedParent + Path.AltDirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsImageExtension(string? ext)
    {
        if (string.IsNullOrWhiteSpace(ext))
            return false;

        return ext.Equals(".png", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".gif", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".webp", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".bmp", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".ico", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".svg", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsAudioExtension(string? ext)
    {
        if (string.IsNullOrWhiteSpace(ext))
            return false;

        return ext.Equals(".mp3", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".ogg", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".wav", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".opus", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".flac", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".m4a", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".aac", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsVideoExtension(string? ext)
    {
        if (string.IsNullOrWhiteSpace(ext))
            return false;

        return ext.Equals(".mp4", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".webm", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".avi", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".mov", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".mkv", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".mpeg", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".mpg", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetCharacterDefinitionsFilePath(string projectPath)
    {
        var gameDirectory = Path.Combine(projectPath, "game");
        if (Directory.Exists(gameDirectory))
            return Path.Combine(gameDirectory, "characters.rpy");

        return Path.Combine(projectPath, "characters.rpy");
    }

    private static string BuildCharacterDefinition(string codeName, string displayName, string color, string? imageTag)
    {
        var arguments = new List<string> { Quote(displayName) };

        if (!string.IsNullOrWhiteSpace(color))
            arguments.Add($"color={Quote(color)}");

        if (!string.IsNullOrWhiteSpace(imageTag))
            arguments.Add($"image={Quote(imageTag!)}");

        return $"define {codeName} = Character({string.Join(", ", arguments)})";
    }

    private static string BuildImageDefinition(string imageTag, string relativePath)
    {
        var normalized = relativePath.Replace('\\', '/');
        return $"image {imageTag} = {Quote(normalized)}";
    }

    private static bool HasImageDefinition(IEnumerable<string> lines, string imageTag)
    {
        foreach (var line in lines)
        {
            var match = ImageDefinitionPrefixRegex.Match(line);
            if (match.Success && string.Equals(match.Groups["tag"].Value, imageTag, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static (string fullPath, string relativePath) ImportCharacterImage(string projectPath, string codeName, string imageSourcePath)
    {
        var normalizedProjectPath = Path.GetFullPath(projectPath);
        var normalizedSourcePath = Path.GetFullPath(imageSourcePath);

        if (normalizedSourcePath.StartsWith(normalizedProjectPath, StringComparison.OrdinalIgnoreCase))
        {
            var existingRelativePath = Path.GetRelativePath(normalizedProjectPath, normalizedSourcePath);
            return (normalizedSourcePath, existingRelativePath);
        }

        var extension = Path.GetExtension(normalizedSourcePath);
        var fileName = $"{codeName}{extension}";
        var imagesDirectory = Directory.Exists(Path.Combine(projectPath, "game"))
            ? Path.Combine(projectPath, "game", "images", "characters")
            : Path.Combine(projectPath, "images", "characters");

        Directory.CreateDirectory(imagesDirectory);

        var targetPath = Path.Combine(imagesDirectory, fileName);
        var suffix = 1;
        while (File.Exists(targetPath))
        {
            fileName = $"{codeName}_{suffix}{extension}";
            targetPath = Path.Combine(imagesDirectory, fileName);
            suffix++;
        }

        File.Copy(normalizedSourcePath, targetPath, overwrite: false);
        var relativePath = Path.GetRelativePath(normalizedProjectPath, targetPath);
        return (targetPath, relativePath);
    }

    private static string Quote(string value)
    {
        var escaped = value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
        return $"\"{escaped}\"";
    }

    private static bool IsFontExtension(string? ext)
    {
        if (string.IsNullOrWhiteSpace(ext))
            return false;

        return ext.Equals(".ttf", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".otf", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".ttc", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".woff", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".woff2", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".fon", StringComparison.OrdinalIgnoreCase);
    }

    private void SaveProject()
    {
        _settings.Settings.ProjectName = _ctx.ProjectName;
        _settings.Settings.ProjectPath = _ctx.ProjectPath;
        _settings.Save();

        Debug.WriteLine(JsonSerializer.Serialize(
            _settings.Settings,
            new JsonSerializerOptions { WriteIndented = true }));
    }

    public void SaveModifiedTabs()
    {
        var savedRpy = false;

        foreach (var tab in Tabs.Where(tab => tab.IsModified).ToArray())
        {
            if (string.IsNullOrWhiteSpace(tab.FilePath))
                continue;

            File.WriteAllText(tab.FilePath, tab.ScriptText);
            tab.MarkSaved();

            if (Path.GetExtension(tab.FilePath).Equals(".rpy", StringComparison.OrdinalIgnoreCase))
                savedRpy = true;
        }

        if (!savedRpy)
            return;

        RefreshStructure(refreshFileTree: false);
        _ = RefreshOpenGraphFromProjectAsync();
    }

    public async Task<bool> OpenProjectFromMenuAsync()
    {
        try
        {
            var selector = _loc.GetService<ProjectSelectorViewModel>()
                ?? throw new InvalidOperationException("Project selector is not registered.");

            var ok = await _windows.ShowDialogAsync(selector);
            if (ok != true || selector.SelectedProject is null)
                return false;

            var projects = _loc.GetService<IProjectApplicationService>()
                ?? throw new InvalidOperationException("Project application service is not registered.");

            var model = projects.OpenExisting(selector.SelectedProject.FolderPath);
            SwitchProject(model);
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Open project from editor error: {ex}");
            await _dialogs.ShowErrorAsync("Open project failed", ex.Message, ex);
            return false;
        }
    }

    private void SwitchProject(ProjectFiles model)
    {
        SelectedTab = null;
        Tabs.Clear();
        _activeGraphViewModel = null;
        MainEditorSelectedIndex = 0;

        _ctx.ProjectName = model.ProjectName;
        _ctx.ProjectPath = model.RootFolder;

        _settings.Settings.ProjectName = model.ProjectName;
        _settings.Settings.ProjectPath = model.RootFolder;
        _settings.Save();

        OnPropertyChanged(nameof(ProjectName));
        OnPropertyChanged(nameof(ProjectPath));
        OnPropertyChanged(nameof(RunButtonText));
        OnPropertyChanged(nameof(StartPointText));

        RefreshStructure();
    }

    private void OpenProjectSettings()
    {
        var vm = _loc.GetService<ProjectSettingsViewModel>()!;
        _windows.ShowWindow(vm);
    }

    private void OpenAppSettings()
    {
        var vm = _loc.GetService<SettingsGUIViewModel>()!;
        _windows.ShowWindow(vm);
    }

    private void OpenIdeSettings()
    {
        var vm = _loc.GetService<IDESettingsViewModel>()!;
        _windows.ShowWindow(vm);
    }

    private void OpenGraphWindow()
    {
        _ = OpenGraphWindowAsync();
    }

    private void OpenStoryTextEditorWindow()
    {
        MainEditorSelectedIndex = 1;
        _ = StoryTextEditorVm.InitializeAsync();
    }

    private void OpenDatabaseLogWindow()
    {
        if (_windows.ActivateWindow<DatabaseLogWindowViewModel>())
            return;

        _windows.ShowWindow(_loc.GetService<DatabaseLogWindowViewModel>()!);
    }

    private void OnStoryTextSourceFilesChanged(IReadOnlyCollection<string> filePaths)
    {
        var changed = new HashSet<string>(
            filePaths.Select(Path.GetFullPath),
            StringComparer.OrdinalIgnoreCase);

        foreach (var tab in Tabs.Where(x => changed.Contains(Path.GetFullPath(x.FilePath))).ToArray())
            tab.RequestReload();

        RefreshStructure(refreshFileTree: false, rebuildStoryIndex: false);
    }

    private async Task OpenGraphWindowAsync()
    {
        if (_windows.ActivateWindow<GraphEditorWindowViewModel>())
            return;

        var vm = _loc.GetService<GraphEditorWindowViewModel>()!;
        _activeGraphViewModel = vm;
        vm.GraphSaved -= OnGraphSaved;
        vm.GraphSaved += OnGraphSaved;
        var snapshot = new ProjectStructureSnapshot(Array.Empty<Character>(), Array.Empty<LabelOutlineItem>(), Array.Empty<StructureLinkItem>());
        if (!string.IsNullOrWhiteSpace(_ctx.ProjectPath))
        {
            try
            {
                snapshot = await _storyStorage.ReadProjectStructureAsync(_ctx.ProjectPath);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Graph snapshot DB read error: {ex}");
                await ShowStoryIndexErrorAsync(ex);
                snapshot = _fallbackStructureReader.Read(_ctx.ProjectPath);
            }

            if (snapshot.Labels.Count == 0)
                snapshot = _fallbackStructureReader.Read(_ctx.ProjectPath);
        }
        vm.LoadSnapshot(snapshot, ProjectName, _ctx.ProjectPath);
        _windows.ShowWindow(vm);
    }

    private void OnGraphSaved(IReadOnlyCollection<string> updatedFiles)
    {
        ReloadOpenTabsAfterGraphSave(updatedFiles);
        RefreshStructure(
            refreshFileTree: true,
            rebuildStoryIndex: true,
            refreshReason: "после сохранения графа обновляется БД label/content для Story Text Editor.");
        _ = RefreshOpenStoryTextEditorAfterGraphSaveAsync();
    }

    private async Task RefreshOpenStoryTextEditorAfterGraphSaveAsync()
    {
        try
        {
            await StoryTextEditorVm.RefreshAfterGraphSavedAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Story text editor refresh after graph save error: {ex}");
        }
    }

    private void ReloadOpenTabsAfterGraphSave(IReadOnlyCollection<string> updatedFiles)
    {
        if (updatedFiles.Count == 0 || string.IsNullOrWhiteSpace(_ctx.ProjectPath))
            return;

        var changedFiles = new HashSet<string>(
            updatedFiles.Select(path => Path.GetFullPath(Path.Combine(_ctx.ProjectPath!, path.Replace('/', Path.DirectorySeparatorChar)))),
            StringComparer.OrdinalIgnoreCase);

        foreach (var tab in Tabs.Where(tab => changedFiles.Contains(Path.GetFullPath(tab.FilePath))).ToArray())
            tab.RequestReload();
    }

    private void OnTabFileSaved(TabItemModel tab)
    {
        tab.MarkSaved();

        if (!Path.GetExtension(tab.FilePath).Equals(".rpy", StringComparison.OrdinalIgnoreCase))
            return;

        RefreshStructure(refreshFileTree: false);
        _ = RefreshOpenGraphFromProjectAsync();
    }

    private async Task RefreshOpenGraphFromProjectAsync()
    {
        if (_activeGraphViewModel is null)
            return;

        try
        {
            await _activeGraphViewModel.RefreshSnapshotFromProjectAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Graph refresh after .rpy save error: {ex}");
        }
    }

    private void RunProject()
    {
        var tab = SelectedTab;
        var breakpointLine = tab?.ActiveBreakpointLine;
        if (tab is not null && breakpointLine is int line && line > 0)
        {
            RunProjectFromLocation(tab.FilePath, line);
            return;
        }

        RunProjectInternal(startLabel: null);
    }

    public void RunProjectFromLabel(string labelName)
    {
        if (string.IsNullOrWhiteSpace(labelName))
            return;

        RunProjectInternal(labelName);
    }

    public void RunProjectFromLocation(string filePath, int line)
    {
        if (string.IsNullOrWhiteSpace(ProjectPath) || string.IsNullOrWhiteSpace(filePath) || line <= 0)
            return;

        var label = FindLaunchLabel(filePath, line);
        if (label is null)
            return;

        RunProjectInternal(label.Name);
    }

    private LabelOutlineItem? FindLaunchLabel(string filePath, int line)
    {
        if (string.IsNullOrWhiteSpace(ProjectPath))
            return null;

        var normalizedFilePath = Path.IsPathRooted(filePath)
            ? Path.GetFullPath(filePath)
            : Path.GetFullPath(Path.Combine(ProjectPath, filePath));

        var relativePath = Path.GetRelativePath(ProjectPath, normalizedFilePath).Replace('\\', '/');
        return LabelList
            .Where(label => string.Equals(label.FilePath, relativePath, StringComparison.OrdinalIgnoreCase))
            .Where(label => line >= label.Line && line <= label.EndLine)
            .OrderByDescending(label => label.Line)
            .FirstOrDefault()
            ?? LabelList
                .Where(label => string.Equals(label.FilePath, relativePath, StringComparison.OrdinalIgnoreCase))
                .Where(label => label.Line <= line)
                .OrderByDescending(label => label.Line)
                .FirstOrDefault();
    }

    private void RunFromHere()
    {
        var tab = SelectedTab;
        var breakpointLine = tab?.ActiveBreakpointLine;
        if (tab is null || breakpointLine is null || breakpointLine.Value <= 0)
            return;

        RunProjectFromLocation(tab.FilePath, breakpointLine.Value);
    }

    private void RunProjectInternal(string? startLabel)
    {
        if (string.IsNullOrWhiteSpace(_ctx.ProjectPath))
            return;

        if (string.IsNullOrWhiteSpace(_ide.RenPySDKPath))
            return;

        try
        {
            var req = new RenPyVisualScriptMVVM.Core.Services.RunProjectRequest(_ide.RenPySDKPath!, _ctx.ProjectPath!);
            req.Run(startLabel);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"RunProject error: {ex}");
        }
    }
}

public sealed class StringToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is string s && !string.IsNullOrWhiteSpace(s)
           ? TryParseBrush(s)
           : Brushes.Transparent;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();

    private static IBrush TryParseBrush(string s)
    {
        try { return Brush.Parse(s); }
        catch { return Brushes.Transparent; }
    }
}
