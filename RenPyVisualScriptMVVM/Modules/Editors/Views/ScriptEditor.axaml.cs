using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using RenPyVisualScriptMVVM.Core.Models;
using RenPyVisualScriptMVVM.Modules.Editors.Models;
using RenPyVisualScriptMVVM.Modules.Editors.Services;
using RenPyVisualScriptMVVM.Modules.Editors.ViewModels;
using RenPyVisualScriptMVVM.Modules.Projects.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace RenPyVisualScriptMVVM.Modules.Editors.Views;

public partial class ScriptEditor : Window
{
    private readonly AudioPreviewPlayer _audioPreviewPlayer = new();

    public ScriptEditor()
    {
        InitializeComponent();
        Closing += (_, _) => _audioPreviewPlayer.Dispose();
    }

    private ScriptEditorViewModel? ViewModel => DataContext as ScriptEditorViewModel;

    private void CharacterList_OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is ListBox { SelectedItem: Character character })
            ViewModel?.NavigateToFile(character.FilePath, character.Line);
    }

    private void LabelList_OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is ListBox { SelectedItem: LabelOutlineItem label })
            ViewModel?.NavigateToFile(label.FilePath, label.Line);
    }

    private void TransitionItems_OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is ListBox { SelectedItem: TransitionPanelItem transition })
            ViewModel?.NavigateToFile(transition.FileName, transition.Line);
    }

    private void TransitionChoice_OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if ((sender as Control)?.DataContext is StructureLinkItem choice)
            ViewModel?.NavigateToFile(choice.FileName, choice.Line);
    }

    private async void AudioPlay_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if ((sender as Control)?.DataContext is not ResourceFileItem resource)
            return;

        try
        {
            _audioPreviewPlayer.Toggle(resource.FullPath);
        }
        catch (Exception ex)
        {
            await ShowMessageBoxAsync("Audio preview error", ex.Message);
        }
    }

    private void AudioStop_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _audioPreviewPlayer.Stop();
    }

    private async void CreateCharacter_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (ViewModel is null)
            return;

        var request = await ShowCreateCharacterDialogAsync();
        if (request is null)
            return;

        try
        {
            ViewModel.CreateCharacter(request);
        }
        catch (Exception ex)
        {
            await ShowMessageBoxAsync("Create character error", ex.Message);
        }
    }

    private async void CreateFile_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (ViewModel is null)
            return;

        var targetNode = (sender as Control)?.DataContext as FileNode ?? ViewModel.SelectedFileNode;
        var fileName = await ShowTextInputDialogAsync("Create file", "File name", "script.rpy");
        if (string.IsNullOrWhiteSpace(fileName))
            return;

        try
        {
            ViewModel.CreateFile(fileName, targetNode);
        }
        catch (Exception ex)
        {
            await ShowMessageBoxAsync("Create file error", ex.Message);
        }
    }

    private async void CreateFolder_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (ViewModel is null)
            return;

        var targetNode = (sender as Control)?.DataContext as FileNode ?? ViewModel.SelectedFileNode;
        var folderName = await ShowTextInputDialogAsync("Create folder", "Folder name", "new_folder");
        if (string.IsNullOrWhiteSpace(folderName))
            return;

        try
        {
            ViewModel.CreateFolder(folderName, targetNode);
        }
        catch (Exception ex)
        {
            await ShowMessageBoxAsync("Create folder error", ex.Message);
        }
    }

    private async System.Threading.Tasks.Task<CharacterCreationRequest?> ShowCreateCharacterDialogAsync()
    {
        var vm = new CharacterCreateDialogViewModel(BuildCharacterImageItems());
        var dialog = new CharacterCreateDialog
        {
            DataContext = vm
        };

        var dialogResult = await dialog.ShowDialog<bool?>(this);
        return dialogResult == true ? vm.Result : null;
    }

    private async System.Threading.Tasks.Task<string?> ShowTextInputDialogAsync(string title, string label, string initialValue)
    {
        var textBox = new TextBox
        {
            Text = initialValue,
            CaretIndex = initialValue.Length
        };

        string? result = null;

        var confirmButton = new Button
        {
            Content = "OK",
            Width = 90,
            IsDefault = true
        };
        var cancelButton = new Button
        {
            Content = "Cancel",
            Width = 90,
            IsCancel = true
        };

        var buttonRow = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            Children = { cancelButton, confirmButton }
        };

        var panel = new StackPanel
        {
            Margin = new Thickness(16),
            Spacing = 10,
            Children =
            {
                new TextBlock { Text = label },
                textBox,
                buttonRow
            }
        };

        var dialog = new Window
        {
            Title = title,
            Width = 420,
            Height = 170,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = panel
        };

        confirmButton.Click += (_, _) =>
        {
            result = textBox.Text;
            dialog.Close();
        };
        cancelButton.Click += (_, _) => dialog.Close();

        await dialog.ShowDialog(this);
        return result;
    }

    private async System.Threading.Tasks.Task ShowMessageBoxAsync(string title, string message)
    {
        var window = new Window
        {
            Title = title,
            Width = 420,
            Height = 180,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new StackPanel
            {
                Margin = new Thickness(16),
                Spacing = 12,
                Children =
                {
                    new TextBlock
                    {
                        Text = message,
                        TextWrapping = Avalonia.Media.TextWrapping.Wrap
                    },
                    new Button
                    {
                        Content = "OK",
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                        Width = 80
                    }
                }
            }
        };

        if (window.Content is StackPanel panel && panel.Children[1] is Button button)
            button.Click += (_, _) => window.Close();

        await window.ShowDialog(this);
    }

    private IReadOnlyList<CharacterImageOption> BuildCharacterImageItems()
    {
        var items = new List<CharacterImageOption>
        {
            new("(none)", null)
        };

        if (ViewModel?.AvailableCharacterImages is null)
            return items;

        foreach (var image in ViewModel.AvailableCharacterImages)
        {
            var imageName = Path.GetFileNameWithoutExtension(image.Name);
            if (items.Any(item => string.Equals(item.Value, imageName, StringComparison.OrdinalIgnoreCase)))
                continue;

            items.Add(new CharacterImageOption(imageName, imageName, image.FullPath));
        }

        return items;
    }
}
