using CommunityToolkit.Mvvm.Input;
using Avalonia.Data.Converters;
using Avalonia.Media;
using RenPyVisualScriptMVVM.Modules.Editors.Models;
using RenPyVisualScriptMVVM.Core.Services;
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
using RenPyVisualScriptMVVM.Modules.Editors.Services;
using RenPyVisualScriptMVVM.Modules.Editors.Services.Interfaces;
using System.Collections.Generic;

namespace RenPyVisualScriptMVVM.Modules.Editors.ViewModels;

public sealed class ScriptEditorViewModel : BaseViewModel
{
    private readonly IProjectContext _ctx;
    private readonly ISettingsService _settings;
    private readonly IDESettings _ide;
    private readonly IWindowService _windows;
    private readonly IEditorNavigationService _editorNavigation;
    private readonly IReadonlyDependencyResolver _loc;
    private readonly RenPyStructureReader _structureReader = new();

    public IRelayCommand SaveCmd { get; }
    public IRelayCommand ShowProjectSetCmd { get; }
    public IRelayCommand ShowAppSettingsCmd { get; }
    public IRelayCommand ShowIdeSettingsCmd { get; }
    public IRelayCommand RunProjectCmd { get; }
    public IRelayCommand RunFromHereCmd { get; }
    public IRelayCommand OpenGraphCmd { get; }
    public IRelayCommand RefreshStructureCmd { get; }

    public ObservableCollection<TabItemModel> Tabs { get; } = new();
    public ObservableCollection<Character> CharacterList { get; } = new();
    public ObservableCollection<LabelOutlineItem> LabelList { get; } = new();
    public ObservableCollection<StructureLinkItem> StructureLinks { get; } = new();
    public ObservableCollection<TransitionPanelItem> TransitionItems { get; } = new();
    public ObservableCollection<ResourceFileItem> ImageResources { get; } = new();
    public ObservableCollection<ResourceFileItem> AudioResources { get; } = new();
    public ObservableCollection<ResourceFileItem> VideoResources { get; } = new();

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
        set => SetProperty(ref _selectedTab, value);
    }

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

    public ScriptEditorViewModel(
        IProjectContext ctx,
        ISettingsService settings,
        IDESettings ide,
        IWindowService windows,
        IEditorNavigationService editorNavigation,
        IReadonlyDependencyResolver? loc = null)
    {
        _ctx = ctx;
        _settings = settings;
        _ide = ide;
        _windows = windows;
        _editorNavigation = editorNavigation;
        _loc = loc ?? Locator.Current;

        FileTreeVm = new FileTreeViewModel(_ctx);

        SaveCmd = new RelayCommand(SaveProject);
        ShowProjectSetCmd = new RelayCommand(OpenProjectSettings);
        ShowAppSettingsCmd = new RelayCommand(OpenAppSettings);
        ShowIdeSettingsCmd = new RelayCommand(OpenIdeSettings);
        RunProjectCmd = new RelayCommand(RunProject);
        RunFromHereCmd = new RelayCommand(RunFromHere);
        OpenGraphCmd = new RelayCommand(OpenGraphWindow);
        RefreshStructureCmd = new RelayCommand(RefreshStructure);

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

    private void RefreshStructure()
    {
        FileTreeVm.Refresh();
        CharacterList.Clear();
        LabelList.Clear();
        StructureLinks.Clear();
        TransitionItems.Clear();
        ImageResources.Clear();
        AudioResources.Clear();
        VideoResources.Clear();

        var snapshot = _structureReader.Read(_ctx.ProjectPath);

        foreach (var character in snapshot.Characters)
            CharacterList.Add(character);

        foreach (var label in snapshot.Labels)
            LabelList.Add(label);

        foreach (var link in snapshot.Links)
            StructureLinks.Add(link);

        foreach (var item in BuildTransitionItems(snapshot.Links))
            TransitionItems.Add(item);

        LoadResourceFiles(_ctx.ProjectPath);

        var projectName = string.IsNullOrWhiteSpace(ProjectName) ? "Проект" : ProjectName;
        StructureSummary = $"{projectName}: {CharacterList.Count} персонажей, {LabelList.Count} label, {StructureLinks.Count} связей, {ImageResources.Count} изображений, {AudioResources.Count} аудио, {VideoResources.Count} видео";
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
                    VideoResources.Add(new ResourceFileItem(file, projectPath));
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

    public void NavigateToFile(string filePath, int? line = null)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return;

        if (!Path.IsPathRooted(filePath) && !string.IsNullOrWhiteSpace(ProjectPath))
            filePath = Path.GetFullPath(Path.Combine(ProjectPath, filePath));

        OpenFileInTab(filePath, Path.GetFileName(filePath), line);
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
                existing.RequestNavigation(line);
                SelectedTab = existing;
                Debug.WriteLine($"Activated existing tab: {existing.FilePath}");
                return;
            }

            var tab = new TabItemModel(
                header,
                filePath,
                closeAction: t => Tabs.Remove(t));

            tab.RequestNavigation(line);
            Tabs.Add(tab);
            SelectedTab = tab;
            Debug.WriteLine($"Opened new tab for: {filePath}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"OpenFileInTab error: {ex}");
        }
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

    private void SaveProject()
    {
        _settings.Settings.ProjectName = _ctx.ProjectName;
        _settings.Settings.ProjectPath = _ctx.ProjectPath;
        _settings.Save();

        Debug.WriteLine(JsonSerializer.Serialize(
            _settings.Settings,
            new JsonSerializerOptions { WriteIndented = true }));
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
        var vm = _loc.GetService<GraphEditorWindowViewModel>()!;
        vm.GraphSaved -= OnGraphSaved;
        vm.GraphSaved += OnGraphSaved;
        var snapshot = _structureReader.Read(_ctx.ProjectPath);
        vm.LoadSnapshot(snapshot, ProjectName, _ctx.ProjectPath);
        _windows.ShowWindow(vm);
    }

    private void OnGraphSaved()
    {
        RefreshStructure();
    }

    private void RunProject()
    {
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
        var snapshot = _structureReader.Read(ProjectPath);

        return snapshot.Labels
            .Where(label => string.Equals(label.FilePath, relativePath, StringComparison.OrdinalIgnoreCase))
            .Where(label => line >= label.Line && line <= label.EndLine)
            .OrderByDescending(label => label.Line)
            .FirstOrDefault()
            ?? snapshot.Labels
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
