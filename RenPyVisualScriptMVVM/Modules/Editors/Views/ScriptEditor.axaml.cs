using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using RenPyVisualScriptMVVM.Core.Models;
using RenPyVisualScriptMVVM.Modules.Editors.Models;
using RenPyVisualScriptMVVM.Modules.Editors.ViewModels;

namespace RenPyVisualScriptMVVM.Modules.Editors.Views;

public partial class ScriptEditor : Window
{
    public ScriptEditor()
    {
        InitializeComponent();
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
}
