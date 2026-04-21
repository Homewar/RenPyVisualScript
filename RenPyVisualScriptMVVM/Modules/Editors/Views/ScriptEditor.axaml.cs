using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
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
    private const string FileNodeDragFormat = "application/x-renpy-file-node-path";
    private readonly AudioPreviewPlayer _audioPreviewPlayer = new();
    private Point? _fileDragStartPoint;
    private FileNode? _fileDragNode;

    public ScriptEditor()
    {
        InitializeComponent();
        LoadOptionalToolbarIcon(RunActivityIcon, RunActivityFallback,
            "avares://RenPyVisualScriptMVVM/Assets/Icons/activity-run.png",
            "avares://RenPyVisualScriptMVVM/Assets/Icons/run.png");
        LoadOptionalToolbarIcon(GraphActivityIcon, GraphActivityFallback,
            "avares://RenPyVisualScriptMVVM/Assets/Icons/activity-graph.png",
            "avares://RenPyVisualScriptMVVM/Assets/Icons/graph.png",
            "avares://RenPyVisualScriptMVVM/Assets/Icons/graph_editor.png");
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

        var targetNode = ResolveFileNodeFromMenu(sender) ?? ViewModel.SelectedFileNode;
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

        var targetNode = ResolveFileNodeFromMenu(sender) ?? ViewModel.SelectedFileNode;
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

    private async void DeleteFileSystemEntryAsync(FileNode? targetNode)
    {
        if (ViewModel is null || targetNode is null)
            return;

        var targetType = targetNode.IsDirectory ? "folder" : "file";
        var confirmed = await ShowConfirmDialogAsync(
            "Delete",
            $"Delete {targetType} '{targetNode.Name}'?");

        if (!confirmed)
            return;

        try
        {
            ViewModel.DeleteFileSystemEntry(targetNode);
        }
        catch (Exception ex)
        {
            await ShowMessageBoxAsync("Delete error", ex.Message);
        }
    }

    private void FileNodeContextMenu_OnOpened(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (ViewModel is null || sender is not ContextMenu contextMenu)
            return;

        var targetNode = ResolveFileNodeFromMenu(contextMenu);
        if (targetNode is not null)
            ViewModel.SelectedFileNode = targetNode;

        var isEditableNode = targetNode is not null && !targetNode.IsRoot;
        foreach (var item in contextMenu.Items.OfType<MenuItem>())
        {
            if (string.Equals(item.Header?.ToString(), "Rename", StringComparison.Ordinal)
                || string.Equals(item.Header?.ToString(), "Delete", StringComparison.Ordinal))
            {
                item.IsEnabled = isEditableNode;
            }
        }
    }

    private void RenameFile_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        RenameFileSystemEntryAsync(ResolveFileNodeFromMenu(sender) ?? ViewModel?.SelectedFileNode);
    }

    private void DeleteFile_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        DeleteFileSystemEntryAsync(ResolveFileNodeFromMenu(sender) ?? ViewModel?.SelectedFileNode);
    }

    private async void RenameFileSystemEntryAsync(FileNode? targetNode)
    {
        if (ViewModel is null || targetNode is null || targetNode.IsRoot)
            return;

        var newName = await ShowTextInputDialogAsync("Rename", "New name", targetNode.Name);
        if (string.IsNullOrWhiteSpace(newName))
            return;

        try
        {
            ViewModel.RenameFileSystemEntry(targetNode, newName);
        }
        catch (Exception ex)
        {
            await ShowMessageBoxAsync("Rename error", ex.Message);
        }
    }

    private void FileTree_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (ViewModel?.SelectedFileNode is null)
            return;

        if (e.Key == Key.Delete)
        {
            DeleteFileSystemEntryAsync(ViewModel.SelectedFileNode);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.F2 && !ViewModel.SelectedFileNode.IsRoot)
        {
            RenameFileSystemEntryAsync(ViewModel.SelectedFileNode);
            e.Handled = true;
        }
    }

    private void FileNode_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if ((sender as Control)?.DataContext is not FileNode node || node.IsRoot)
            return;

        var point = e.GetCurrentPoint(this);
        if (!point.Properties.IsLeftButtonPressed)
            return;

        _fileDragStartPoint = point.Position;
        _fileDragNode = node;
    }

    private async void FileNode_OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_fileDragStartPoint is null || _fileDragNode is null)
            return;

        var point = e.GetCurrentPoint(this);
        if (!point.Properties.IsLeftButtonPressed)
        {
            _fileDragStartPoint = null;
            _fileDragNode = null;
            return;
        }

        var distance = point.Position - _fileDragStartPoint.Value;
        if (Math.Abs(distance.X) < 6 && Math.Abs(distance.Y) < 6)
            return;

        var dragNode = _fileDragNode;
        _fileDragStartPoint = null;
        _fileDragNode = null;

        var data = new DataObject();
        data.Set(FileNodeDragFormat, dragNode.FullPath);

        await DragDrop.DoDragDrop(e, data, DragDropEffects.Move);
        e.Handled = true;
    }

    private void FileNode_OnDragOver(object? sender, DragEventArgs e)
    {
        if (ViewModel is null
            || (sender as Control)?.DataContext is not FileNode targetNode
            || e.Data.Get(FileNodeDragFormat) is not string sourcePath)
        {
            e.DragEffects = DragDropEffects.None;
            e.Handled = true;
            return;
        }

        e.DragEffects = ViewModel.CanMoveFileSystemEntry(sourcePath, targetNode)
            ? DragDropEffects.Move
            : DragDropEffects.None;
        e.Handled = true;
    }

    private async void FileNode_OnDrop(object? sender, DragEventArgs e)
    {
        if (ViewModel is null
            || (sender as Control)?.DataContext is not FileNode targetNode
            || e.Data.Get(FileNodeDragFormat) is not string sourcePath)
            return;

        var sourceNode = ViewModel.FileTreeVm.FindNode(sourcePath);
        if (sourceNode is null)
            return;

        try
        {
            ViewModel.MoveFileSystemEntry(sourceNode, targetNode);
        }
        catch (Exception ex)
        {
            await ShowMessageBoxAsync("Move error", ex.Message);
        }

        e.Handled = true;
    }

    private async void CreateFile_FromNode(FileNode targetNode)
    {
        if (ViewModel is null)
            return;

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

    private static FileNode? ResolveFileNodeFromMenu(object? source)
    {
        if (source is ContextMenu contextMenu)
        {
            if (contextMenu.PlacementTarget is StyledElement { DataContext: FileNode contextNode })
                return contextNode;
        }

        if (source is MenuItem { Parent: ContextMenu parentMenu })
        {
            if (parentMenu.PlacementTarget is StyledElement { DataContext: FileNode menuNode })
                return menuNode;
        }

        return (source as StyledElement)?.DataContext as FileNode;
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

    private async System.Threading.Tasks.Task<bool> ShowConfirmDialogAsync(string title, string message)
    {
        var result = false;

        var confirmButton = new Button
        {
            Content = "Delete",
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
                new TextBlock
                {
                    Text = message,
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap
                },
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
            result = true;
            dialog.Close();
        };
        cancelButton.Click += (_, _) => dialog.Close();

        await dialog.ShowDialog(this);
        return result;
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

    private static void LoadOptionalToolbarIcon(Image image, TextBlock fallback, params string[] assetUris)
    {
        foreach (var assetUri in assetUris)
        {
            try
            {
                var uri = new Uri(assetUri);
                if (!AssetLoader.Exists(uri))
                    continue;

                using var stream = AssetLoader.Open(uri);
                image.Source = new Bitmap(stream);
                image.IsVisible = true;
                fallback.IsVisible = false;
                return;
            }
            catch
            {
                image.Source = null;
            }
        }

        image.IsVisible = false;
        fallback.IsVisible = true;
    }
}
