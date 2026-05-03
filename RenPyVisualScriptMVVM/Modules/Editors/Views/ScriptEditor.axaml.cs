using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using RenPyVisualScriptMVVM.Core.Models;
using RenPyVisualScriptMVVM.Modules.Editors.Models;
using RenPyVisualScriptMVVM.Modules.Editors.Services;
using RenPyVisualScriptMVVM.Modules.Editors.Services.Interfaces;
using RenPyVisualScriptMVVM.Modules.Editors.ViewModels;
using RenPyVisualScriptMVVM.Modules.GraphEditor.Views;
using RenPyVisualScriptMVVM.Modules.Projects.Models;
using RenPyVisualScriptMVVM.Modules.StoryEditor.Views;
using Splat;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace RenPyVisualScriptMVVM.Modules.Editors.Views;

public partial class ScriptEditor : Window
{
    private const string FileNodeDragFormat = "application/x-renpy-file-node-path";
    private readonly AudioPreviewPlayer _audioPreviewPlayer = new();
    private readonly IEditorDialogService _dialogService;
    private Point? _fileDragStartPoint;
    private FileNode? _fileDragNode;
    private bool _isCloseConfirmed;
    private bool _isClosePromptOpen;

    public ScriptEditor()
    {
        InitializeComponent();
        _dialogService = Locator.Current.GetService<IEditorDialogService>() ?? new EditorDialogService();
        LoadOptionalToolbarIcon(RunActivityIcon, RunActivityFallback,
            "avares://RenPyVisualScriptMVVM/Assets/Icons/activity-run.png",
            "avares://RenPyVisualScriptMVVM/Assets/Icons/run.png");
        LoadOptionalToolbarIcon(GraphActivityIcon, GraphActivityFallback,
            "avares://RenPyVisualScriptMVVM/Assets/Icons/activity-graph.png",
            "avares://RenPyVisualScriptMVVM/Assets/Icons/graph.png",
            "avares://RenPyVisualScriptMVVM/Assets/Icons/graph_editor.png");
        Closing += OnClosing;
        Closed += (_, _) =>
        {
            CloseLinkedEditorWindows();
            _audioPreviewPlayer.Dispose();
        };
    }

    private ScriptEditorViewModel? ViewModel => DataContext as ScriptEditorViewModel;

    private async void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_isCloseConfirmed || ViewModel is not { HasUnsavedChanges: true } vm)
            return;

        e.Cancel = true;
        if (_isClosePromptOpen)
            return;

        if (!await ConfirmSaveChangesAsync(vm))
            return;

        _isCloseConfirmed = true;
        Close();
    }

    private async void OpenProject_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (ViewModel is not { } vm)
            return;

        if (!await ConfirmSaveChangesAsync(vm))
            return;

        if (!await vm.OpenProjectFromMenuAsync())
            return;

        CloseLinkedEditorWindows();
    }

    private async System.Threading.Tasks.Task<bool> ConfirmSaveChangesAsync(ScriptEditorViewModel vm)
    {
        if (!vm.HasUnsavedChanges)
            return true;

        SaveChangesDialogResult? result;
        try
        {
            _isClosePromptOpen = true;
            var dialog = new SaveChangesDialog(vm.UnsavedTabCount);
            result = await dialog.ShowDialog<SaveChangesDialogResult?>(this);
        }
        finally
        {
            _isClosePromptOpen = false;
        }

        if (result is null or SaveChangesDialogResult.Cancel)
            return false;

        if (result == SaveChangesDialogResult.Save)
        {
            try
            {
                vm.SaveModifiedTabs();
            }
            catch (Exception ex)
            {
                await _dialogService.ShowMessageAsync(this, "Save failed", ex.Message);
                return false;
            }
        }

        return true;
    }

    private void CloseLinkedEditorWindows()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime lifetime)
            return;

        foreach (var window in lifetime.Windows.ToArray())
        {
            if (ReferenceEquals(window, this))
                continue;

            if (window is GraphEditorWindow)
                window.Close();
        }
    }

    private void CharacterList_OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is ListBox { SelectedItem: Character character })
            ViewModel?.NavigateToFile(character.FilePath, character.Line);
    }

    private void CharacterList_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (ViewModel?.IsStoryTabVisible != true)
            return;

        Dispatcher.UIThread.Post(
            () => StoryTextEditorHost.FocusStoryTextEditor(),
            DispatcherPriority.Input);
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
            await _dialogService.ShowMessageAsync(this, "Audio preview error", ex.Message);
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

        var request = await _dialogService.ShowCreateCharacterDialogAsync(this, BuildCharacterImageItems());
        if (request is null)
            return;

        try
        {
            ViewModel.CreateCharacter(request);
        }
        catch (Exception ex)
        {
            await _dialogService.ShowMessageAsync(this, "Create character error", ex.Message);
        }
    }

    private async void CreateFile_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (ViewModel is null)
            return;

        var targetNode = ResolveFileNodeFromMenu(sender) ?? ViewModel.SelectedFileNode;
        var fileName = await _dialogService.ShowTextInputDialogAsync(this, "Create file", "File name", "script.rpy");
        if (string.IsNullOrWhiteSpace(fileName))
            return;

        try
        {
            ViewModel.CreateFile(fileName, targetNode);
        }
        catch (Exception ex)
        {
            await _dialogService.ShowMessageAsync(this, "Create file error", ex.Message);
        }
    }

    private async void CreateFolder_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (ViewModel is null)
            return;

        var targetNode = ResolveFileNodeFromMenu(sender) ?? ViewModel.SelectedFileNode;
        var folderName = await _dialogService.ShowTextInputDialogAsync(this, "Create folder", "Folder name", "new_folder");
        if (string.IsNullOrWhiteSpace(folderName))
            return;

        try
        {
            ViewModel.CreateFolder(folderName, targetNode);
        }
        catch (Exception ex)
        {
            await _dialogService.ShowMessageAsync(this, "Create folder error", ex.Message);
        }
    }

    private async void DeleteFileSystemEntryAsync(FileNode? targetNode)
    {
        if (ViewModel is null || targetNode is null)
            return;

        var targetType = targetNode.IsDirectory ? "folder" : "file";
        var confirmed = await _dialogService.ShowConfirmDialogAsync(
            this,
            "Delete",
            $"Delete {targetType} '{targetNode.Name}'?",
            "Delete");

        if (!confirmed)
            return;

        try
        {
            ViewModel.DeleteFileSystemEntry(targetNode);
        }
        catch (Exception ex)
        {
            await _dialogService.ShowMessageAsync(this, "Delete error", ex.Message);
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

        var newName = await _dialogService.ShowTextInputDialogAsync(this, "Rename", "New name", targetNode.Name);
        if (string.IsNullOrWhiteSpace(newName))
            return;

        try
        {
            ViewModel.RenameFileSystemEntry(targetNode, newName);
        }
        catch (Exception ex)
        {
            await _dialogService.ShowMessageAsync(this, "Rename error", ex.Message);
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
            await _dialogService.ShowMessageAsync(this, "Move error", ex.Message);
        }

        e.Handled = true;
    }

    private async void CreateFile_FromNode(FileNode targetNode)
    {
        if (ViewModel is null)
            return;

        var fileName = await _dialogService.ShowTextInputDialogAsync(this, "Create file", "File name", "script.rpy");
        if (string.IsNullOrWhiteSpace(fileName))
            return;

        try
        {
            ViewModel.CreateFile(fileName, targetNode);
        }
        catch (Exception ex)
        {
            await _dialogService.ShowMessageAsync(this, "Create file error", ex.Message);
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
