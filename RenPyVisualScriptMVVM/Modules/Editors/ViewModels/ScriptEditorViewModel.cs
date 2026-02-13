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

namespace RenPyVisualScriptMVVM.Modules.Editors.ViewModels;

public sealed class ScriptEditorViewModel : BaseViewModel
{
    private readonly IProjectContext _ctx;
    private readonly ISettingsService _settings;
    private readonly IWindowService _windows;
    private readonly IReadonlyDependencyResolver _loc;

    public IRelayCommand SaveCmd { get; }
    public IRelayCommand ShowProjectSetCmd { get; }
    public IRelayCommand ShowAppSettingsCmd { get; }

    public ObservableCollection<TabItemModel> Tabs { get; } = new();

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

    public ObservableCollection<Character> CharacterList { get; }

    public ScriptEditorViewModel(
        IProjectContext ctx,
        ISettingsService settings,
        IWindowService windows,
        IReadonlyDependencyResolver? loc = null)
    {
        _ctx = ctx;
        _settings = settings;
        _windows = windows;
        _loc = loc ?? Locator.Current;

        FileTreeVm = new FileTreeViewModel(_ctx);

        SaveCmd = new RelayCommand(SaveProject);
        ShowProjectSetCmd = new RelayCommand(OpenProjectSettings);
        ShowAppSettingsCmd = new RelayCommand(OpenAppSettings);

        CharacterList = InitCharacters();

        Debug.WriteLine("ScriptEditorViewModel initialized");
    }

    private void OpenFileInTab(FileNode node)
    {
        try
        {
            if (!File.Exists(node.FullPath))
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

    private static ObservableCollection<Character> InitCharacters() =>
        new()
        {
            new("Alice", "#FF0000", "A"),
            new("Bob",   "Blue",    "B"),
            new("Clara", "Green",   "C"),
            new("David", "Yellow",  "D")
        };
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