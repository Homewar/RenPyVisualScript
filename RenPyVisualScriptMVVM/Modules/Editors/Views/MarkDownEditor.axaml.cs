using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;

namespace RenPyVisualScriptMVVM.Modules.Editors.Views;

public partial class MarkDownEditor : Window
{
    public MarkDownEditor()
    {
        InitializeComponent();
        AddHandler(DragDrop.DragOverEvent, DragOver);
        AddHandler(DragDrop.DropEvent, Drop);
    }

    private void DragOver(object? sender, DragEventArgs e)
    { 
        e.DragEffects = DragDropEffects.Copy;
    }

    private void Drop(object? sender, DragEventArgs e)
    { 
        
    }
}