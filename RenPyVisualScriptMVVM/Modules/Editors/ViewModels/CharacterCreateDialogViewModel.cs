using CommunityToolkit.Mvvm.Input;
using RenPyVisualScriptMVVM.Modules.Editors.Models;
using RenPyVisualScriptMVVM.Modules.Shell.Services.Interfaces;
using RenPyVisualScriptMVVM.Modules.Shell.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace RenPyVisualScriptMVVM.Modules.Editors.ViewModels;

public sealed class CharacterCreateDialogViewModel : BaseViewModel, ICloseRequest
{
    internal CharacterCreationRequest? Result { get; private set; }

    private string? _codeName;
    public string? CodeName
    {
        get => _codeName;
        set
        {
            if (SetProperty(ref _codeName, value))
                CreateCmd.NotifyCanExecuteChanged();
        }
    }

    private string? _displayName;
    public string? DisplayName
    {
        get => _displayName;
        set
        {
            if (SetProperty(ref _displayName, value))
                CreateCmd.NotifyCanExecuteChanged();
        }
    }

    private string? _color = "#ffffff";
    public string? Color
    {
        get => _color;
        set => SetProperty(ref _color, value);
    }

    private CharacterImageOption? _selectedImage;
    public CharacterImageOption? SelectedImage
    {
        get => _selectedImage;
        set
        {
            if (SetProperty(ref _selectedImage, value) && value is not null && !string.IsNullOrWhiteSpace(value.SourcePath))
            {
                LocalImagePath = null;
            }

            OnPropertyChanged(nameof(PreviewImagePath));
        }
    }

    private string? _localImagePath;
    public string? LocalImagePath
    {
        get => _localImagePath;
        set
        {
            if (SetProperty(ref _localImagePath, value) && !string.IsNullOrWhiteSpace(value))
                SelectedImage = ImageOptions.Count > 0 ? ImageOptions[0] : null;

            OnPropertyChanged(nameof(PreviewImagePath));
        }
    }

    public string? PreviewImagePath
        => !string.IsNullOrWhiteSpace(LocalImagePath)
            ? LocalImagePath
            : SelectedImage?.SourcePath;

    private bool _isPcImageMode;
    public bool IsPcImageMode
    {
        get => _isPcImageMode;
        set
        {
            if (!SetProperty(ref _isPcImageMode, value))
                return;

            if (IsProjectImageMode)
                LocalImagePath = null;
            else if (ImageOptions.Count > 0)
                SelectedImage = ImageOptions[0];

            OnPropertyChanged(nameof(IsProjectImageMode));
            OnPropertyChanged(nameof(PreviewImagePath));
        }
    }

    public bool IsProjectImageMode => !IsPcImageMode;

    public ObservableCollection<CharacterImageOption> ImageOptions { get; }

    public RelayCommand CreateCmd { get; }
    public RelayCommand CancelCmd { get; }

    public event Action<bool?>? RequestClose;

    public CharacterCreateDialogViewModel(IEnumerable<CharacterImageOption> imageOptions)
    {
        ImageOptions = new ObservableCollection<CharacterImageOption>(imageOptions);
        if (ImageOptions.Count > 0)
            SelectedImage = ImageOptions[0];

        CreateCmd = new RelayCommand(Create, CanCreate);
        CancelCmd = new RelayCommand(() => RequestClose?.Invoke(false));
    }

    private bool CanCreate()
        => !string.IsNullOrWhiteSpace(CodeName)
           && !string.IsNullOrWhiteSpace(DisplayName);

    private void Create()
    {
        Result = new CharacterCreationRequest
        {
            CodeName = CodeName?.Trim() ?? "",
            DisplayName = DisplayName?.Trim() ?? "",
            Color = Color?.Trim() ?? "",
            ImageTag = IsPcImageMode && !string.IsNullOrWhiteSpace(LocalImagePath)
                ? (CodeName?.Trim() ?? "")
                : SelectedImage?.Value,
            ImageSourcePath = IsPcImageMode && !string.IsNullOrWhiteSpace(LocalImagePath)
                ? LocalImagePath
                : SelectedImage?.SourcePath
        };

        RequestClose?.Invoke(true);
    }
}
