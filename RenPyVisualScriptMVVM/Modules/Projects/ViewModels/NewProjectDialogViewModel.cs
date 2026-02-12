using Avalonia.Media;
using CommunityToolkit.Mvvm.Input;
using RenPyVisualScriptMVVM.Core.Models;
using RenPyVisualScriptMVVM.Core.Services.Interfaces;
using RenPyVisualScriptMVVM.Core.ViewModels;
using System;
using System.Collections.ObjectModel;

namespace RenPyVisualScriptMVVM.Modules.Projects.ViewModels;

public sealed class NewProjectDialogViewModel : BaseViewModel , ICloseRequest
{
    internal VisualNovellProjectData? Result { get; private set; }

    private string? _projectName;
    public string? ProjectName
    {
        get => _projectName;
        set
        {
            if (SetProperty(ref _projectName, value))
                ((RelayCommand)OkCmd).NotifyCanExecuteChanged();
        }
    }

    // -------- Resolution --------
    public ObservableCollection<ResolutionItem> Resolutions { get; } = new()
    {
        new(1280, 720,  "1280×720 (HD)"),
        new(1366, 768,  "1366×768"),
        new(1600, 900,  "1600×900"),
        new(1920, 1080, "1920×1080 (Full HD)"),
        new(2560, 1440, "2560×1440 (QHD)"),
        new(3840, 2160, "3840×2160 (4K)"),
    };

    private ResolutionItem? _selectedResolution;
    public ResolutionItem? SelectedResolution
    {
        get => _selectedResolution;
        set => SetProperty(ref _selectedResolution, value);
    }

    // -------- Accent Color --------
    public ObservableCollection<ColorItem> AccentColors { get; } = new()
    {
        new("#00B8C3", "Cyan"),
        new("#FF7A00", "Orange"),
        new("#E91E63", "Pink"),
        new("#9C27B0", "Purple"),
        new("#4CAF50", "Green"),
        new("#2196F3", "Blue"),
        new("#FFFFFF", "White"),
    };

    private ColorItem? _selectedAccentColor;
    public ColorItem? SelectedAccentColor
    {
        get => _selectedAccentColor;
        set
        {
            if (SetProperty(ref _selectedAccentColor, value))
                OnPropertyChanged(nameof(SelectedAccentColorBrush));
        }
    }

    // Удобно для превью в XAML
    public IBrush SelectedAccentColorBrush
        => SelectedAccentColor?.Brush ?? Brushes.Transparent;

    // -------- Language --------
    public ObservableCollection<LanguageItem> Languages { get; } = new()
    {
        new("russian", "Русский"),
        new("english", "English"),
        new("spanish", "Español"),
        new("french",  "Français"),
        new("german",  "Deutsch"),
        new("italian", "Italiano"),
        new("japanese","日本語"),
    };

    private LanguageItem? _selectedLanguage;
    public LanguageItem? SelectedLanguage
    {
        get => _selectedLanguage;
        set => SetProperty(ref _selectedLanguage, value);
    }

    // -------- Commands --------
    public IRelayCommand OkCmd { get; }
    public IRelayCommand CancelCmd { get; }

    public event Action<bool?>? RequestClose;

    public NewProjectDialogViewModel()
    {
        // значения по умолчанию
        SelectedResolution = Resolutions[3];          // 1920×1080
        SelectedAccentColor = AccentColors[0];        // #00B8C3
        SelectedLanguage = Languages[0];              // russian

        OkCmd = new RelayCommand(OnOk, CanOk);
        CancelCmd = new RelayCommand(() => RequestClose?.Invoke(false));
    }

    private void OnOk()
    {
        // CanOk гарантирует, что всё заполнено
        Result = new VisualNovellProjectData
        {
            Name = ProjectName!.Trim(), // важно
            Resolution = new Tuple<int, int>(SelectedResolution!.Width, SelectedResolution!.Height),
            color = SelectedAccentColor!.Color,
            Language = SelectedLanguage!.Code
        };
        RequestClose?.Invoke(true);
    }

    private bool CanOk()
        => !string.IsNullOrWhiteSpace(ProjectName)
           && SelectedResolution is not null
           && SelectedAccentColor is not null
           && SelectedLanguage is not null;
}

// Models for ComboBox binding
public sealed record ResolutionItem(int Width, int Height, string Title)
{
    public override string ToString() => Title;
}

public sealed class ColorItem
{
    public string Hex { get; }
    public string Name { get; }
    public Color Color { get; }
    public IBrush Brush { get; }

    public ColorItem(string hex, string name)
    {
        Hex = hex.StartsWith("#") ? hex : $"#{hex}";
        Name = name;

        // Avalonia умеет парсить hex через Color.Parse
        Color = Color.Parse(Hex);
        Brush = new SolidColorBrush(Color);
    }

    public override string ToString() => $"{Name} ({Hex})";
}

public sealed record LanguageItem(string Code, string Title)
{
    public override string ToString() => Title;
}