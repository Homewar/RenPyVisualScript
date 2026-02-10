using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using RenPyVisualScriptMVVM.Modules.Editors.Models;
using RenPyVisualScriptMVVM.Core.Services;
using RenPyVisualScriptMVVM.Modules.Editors.ViewModels;
using Splat;

namespace RenPyVisualScriptMVVM.Modules.Editors.Views;

public partial class ScriptEditor : Window
{
    public ScriptEditor()
    {
        InitializeComponent();
        DataContext = Locator.Current.GetService<ScriptEditorViewModel>();
    }
}