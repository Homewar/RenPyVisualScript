using Avalonia.Controls;
using Avalonia.Platform.Storage;
using RenPyVisualScriptMVVM.Modules.Editors.ViewModels;
using System.Linq;

namespace RenPyVisualScriptMVVM.Modules.Editors.Views;

public partial class CharacterCreateDialog : Window
{
    private CharacterCreateDialogViewModel? _vm;

    public CharacterCreateDialog()
    {
        InitializeComponent();

        DataContextChanged += (_, _) => WireViewModel();
        Opened += (_, _) => WireViewModel();
    }

    private void WireViewModel()
    {
        if (_vm is not null)
            _vm.RequestClose -= OnRequestClose;

        _vm = DataContext as CharacterCreateDialogViewModel;
        if (_vm is not null)
            _vm.RequestClose += OnRequestClose;
    }

    private void OnRequestClose(bool? dialogResult) => Close(dialogResult);

    private async void BrowseImage_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_vm is null || StorageProvider is null)
            return;

        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Choose character image",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Images")
                {
                    Patterns = ["*.png", "*.jpg", "*.jpeg", "*.webp", "*.bmp", "*.gif"]
                }
            ]
        });

        var file = files?.FirstOrDefault();
        if (file is null)
            return;

        var path = file.TryGetLocalPath();
        if (!string.IsNullOrWhiteSpace(path))
            _vm.LocalImagePath = path;
    }
}
