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

namespace RenPyVisualScriptMVVM.Modules.Editors.ViewModels;

public sealed class ScriptEditorViewModel : BaseViewModel
{
    private readonly IProjectContext _ctx;
    private readonly ISettingsService _settings;
    private readonly IDESettings _ide;
    private readonly IWindowService _windows;
    private readonly IReadonlyDependencyResolver _loc;
    private readonly RenPyStructureReader _structureReader = new();

    public IRelayCommand SaveCmd { get; }
    public IRelayCommand ShowProjectSetCmd { get; }
    public IRelayCommand ShowAppSettingsCmd { get; }
    public IRelayCommand RunProjectCmd { get; }
    public IRelayCommand OpenGraphCmd { get; }
    public IRelayCommand RefreshStructureCmd { get; }

    public ObservableCollection<TabItemModel> Tabs { get; } = new();
    public ObservableCollection<Character> CharacterList { get; } = new();
    public ObservableCollection<LabelOutlineItem> LabelList { get; } = new();
    public ObservableCollection<StructureLinkItem> StructureLinks { get; } = new();

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
        IReadonlyDependencyResolver? loc = null)
    {
        _ctx = ctx;
        _settings = settings;
        _ide = ide;
        _windows = windows;
        _loc = loc ?? Locator.Current;

        FileTreeVm = new FileTreeViewModel(_ctx);

        SaveCmd = new RelayCommand(SaveProject);
        ShowProjectSetCmd = new RelayCommand(OpenProjectSettings);
        ShowAppSettingsCmd = new RelayCommand(OpenAppSettings);
        RunProjectCmd = new RelayCommand(RunProject);
        OpenGraphCmd = new RelayCommand(OpenGraphWindow);
        RefreshStructureCmd = new RelayCommand(RefreshStructure);

        _ctx.PropertyChanged += OnProjectContextChanged;
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

    private void RefreshStructure()
    {
        CharacterList.Clear();
        LabelList.Clear();
        StructureLinks.Clear();

        var snapshot = _structureReader.Read(_ctx.ProjectPath);

        foreach (var character in snapshot.Characters)
            CharacterList.Add(character);

        foreach (var label in snapshot.Labels)
            LabelList.Add(label);

        foreach (var link in snapshot.Links)
            StructureLinks.Add(link);

        var projectName = string.IsNullOrWhiteSpace(ProjectName) ? "Проект" : ProjectName;
        StructureSummary = $"{projectName}: {CharacterList.Count} персонажей, {LabelList.Count} label, {StructureLinks.Count} связей";
    }

    private void OpenFileInTab(FileNode node)
    {
        try
        {
            if (!File.Exists(node.FullPath))
                return;

            var ext = Path.GetExtension(node.FullPath);
            if (IsImageExtension(ext))
                return;

            var existing = Tabs.FirstOrDefault(t => t.FilePath == node.FullPath);
            if (existing != null)
            {
                SelectedTab = existing;
                Debug.WriteLine($"Activated existing tab: {existing.FilePath}");
                return;
            }

            var tab = new TabItemModel(
                node.Name,
                node.FullPath,
                closeAction: t => Tabs.Remove(t));

            Tabs.Add(tab);
            SelectedTab = tab;
            Debug.WriteLine($"Opened new tab for: {node.FullPath}");
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

    private void OpenGraphWindow()
    {
        var vm = _loc.GetService<GraphEditorWindowViewModel>()!;
        var snapshot = _structureReader.Read(_ctx.ProjectPath);
        vm.LoadSnapshot(snapshot, ProjectName, _ctx.ProjectPath);
        _windows.ShowWindow(vm);
    }

    private void RunProject()
    {
        if (string.IsNullOrWhiteSpace(_ctx.ProjectPath))
            return;

        if (string.IsNullOrWhiteSpace(_ide.RenPySDKPath))
            return;

        try
        {
            var req = new RenPyVisualScriptMVVM.Core.Services.RunProjectRequest(_ide.RenPySDKPath!, _ctx.ProjectPath!);
            req.Run();
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
