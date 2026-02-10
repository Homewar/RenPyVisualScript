using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using RenPyVisualScriptMVVM.Modules.Settings.Models;
using RenPyVisualScriptMVVM.Modules.Settings.ViewModels;
using System;

namespace RenPyVisualScriptMVVM.Modules.Settings.Views;

public partial class ProjectSettings : Window
{
    private readonly string _settingsFolder;
    public ProjectSettings()
    {
        InitializeComponent();
        _settingsFolder = _settingsFolder; 
    }
}