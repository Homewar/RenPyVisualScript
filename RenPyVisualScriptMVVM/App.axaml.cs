using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using RenPyVisualScriptMVVM.Core.Services;
using RenPyVisualScriptMVVM.Core.Services.Interfaces;
using RenPyVisualScriptMVVM.Modules.Editors.Services;
using RenPyVisualScriptMVVM.Modules.Editors.Services.Interfaces;
using RenPyVisualScriptMVVM.Modules.Editors.ViewModels;
using RenPyVisualScriptMVVM.Modules.GraphEditor.ViewModels;
using RenPyVisualScriptMVVM.Modules.Projects.ViewModels;
using RenPyVisualScriptMVVM.Modules.Settings.Services;
using RenPyVisualScriptMVVM.Modules.Settings.ViewModels;
using RenPyVisualScriptMVVM.Modules.Shell.Services;
using RenPyVisualScriptMVVM.Modules.Shell.Services.Interfaces;
using RenPyVisualScriptMVVM.Modules.Shell.ViewModels;
using RenPyVisualScriptMVVM.Modules.Shell.Views;
using Splat;
using System;
using System.IO.Abstractions;
using System.Threading.Tasks;

namespace RenPyVisualScriptMVVM;

public partial class App : Application
{
    private static bool _isShowingCrashDialog;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktopLifetime)
        {
            base.OnFrameworkInitializationCompleted();
            return;
        }

        var desktop = desktopLifetime;
        RegisterGlobalExceptionHandlers(desktop);

        var loc = Locator.CurrentMutable;

        var ctx = new ProjectContext();
        var settingsSvc = new JsonSettingsService(ctx);

        loc.RegisterConstant<IProjectContext>(ctx);
        loc.RegisterConstant<ISettingsService>(settingsSvc);

        loc.RegisterConstant<IWindowService>(new WindowService(desktop));
        loc.RegisterConstant<IFileSystem>(new FileSystem());
        loc.RegisterConstant<IEditorNavigationService>(new EditorNavigationService());

        loc.RegisterLazySingleton<IProjectStorage>(() =>
            new FileSystemProjectStorage(Locator.Current.GetService<IFileSystem>()!));

        loc.RegisterLazySingleton<ISettingsIDE>(() => new IDEsettingsStoreService());

        var ideStore = Locator.Current.GetService<ISettingsIDE>()!;
        var ide = ideStore.LoadAsync().GetAwaiter().GetResult();

        loc.RegisterConstant<RenPyVisualScriptMVVM.Core.Models.IDESettings>(ide);

        loc.RegisterLazySingleton<IProjectCreator>(() =>
            new ProjectCreator(
                Locator.Current.GetService<RenPyVisualScriptMVVM.Core.Models.IDESettings>()!,
                Locator.Current.GetService<IProjectStorage>()!));

        loc.RegisterLazySingleton<IProjectApplicationService>(() =>
            new ProjectApplicationService(
                Locator.Current.GetService<IProjectCreator>()!,
                Locator.Current.GetService<IProjectStorage>()!));

        loc.RegisterLazySingleton<MainWindowViewModel>(() =>
            new MainWindowViewModel(
                ctx,
                Locator.Current.GetService<IProjectApplicationService>()!,
                Locator.Current.GetService<IWindowService>()!,
                settingsSvc,
                () => Locator.Current.GetService<NewProjectDialogViewModel>()!,
                () => Locator.Current.GetService<ProjectSelectorViewModel>()!,
                () => Locator.Current.GetService<ScriptEditorViewModel>()!));

        loc.Register<NewProjectDialogViewModel>(() => new NewProjectDialogViewModel());
        loc.Register<ProjectSelectorViewModel>(() => new ProjectSelectorViewModel(settingsSvc));

        loc.Register<ScriptEditorViewModel>(() =>
            new ScriptEditorViewModel(
                Locator.Current.GetService<IProjectContext>()!,
                Locator.Current.GetService<ISettingsService>()!,
                Locator.Current.GetService<RenPyVisualScriptMVVM.Core.Models.IDESettings>()!,
                Locator.Current.GetService<IWindowService>()!,
                Locator.Current.GetService<IEditorNavigationService>()!,
                Locator.Current));

        loc.Register<FileTreeViewModel>(() =>
            new FileTreeViewModel(Locator.Current.GetService<IProjectContext>()!));

        loc.Register<SettingsGUIViewModel>(() =>
            new SettingsGUIViewModel(Locator.Current.GetService<IProjectContext>()!));

        loc.Register<ProjectSettingsViewModel>(() =>
            new ProjectSettingsViewModel(Locator.Current.GetService<IProjectContext>()!));

        loc.Register<IDESettingsViewModel>(() =>
            new IDESettingsViewModel(
                Locator.Current.GetService<ISettingsIDE>()!,
                Locator.Current.GetService<RenPyVisualScriptMVVM.Core.Models.IDESettings>()!));

        loc.Register<GraphEditorWindowViewModel>(() => new GraphEditorWindowViewModel());

        var mainWindow = new MainWindow
        {
            DataContext = Locator.Current.GetService<MainWindowViewModel>()!
        };

        desktop.Exit += (_, _) => settingsSvc.Save();
        desktop.MainWindow = mainWindow;
        mainWindow.Show();

        if (!IDEsettingsStoreService.IsValidSdkPath(ide.RenPySDKPath))
        {
            var vm = new SDKrequestViewModel(ideStore, isFirstRun: true);
            var win = new SDKrequest { DataContext = vm };
            vm.RequestClose += () => win.Close();
            win.ShowDialog(mainWindow);
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void RegisterGlobalExceptionHandlers(IClassicDesktopStyleApplicationLifetime desktop)
    {
        Dispatcher.UIThread.UnhandledException += (_, e) =>
        {
            e.Handled = true;
            ShowCrashDialog(desktop, e.Exception, "UI thread exception");
        };

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            var exception = e.ExceptionObject as Exception
                ?? new Exception(e.ExceptionObject?.ToString() ?? "Unknown unhandled exception.");
            ShowCrashDialog(desktop, exception, "Unhandled exception");
        };

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            e.SetObserved();
            ShowCrashDialog(desktop, e.Exception, "Background task exception");
        };
    }

    private static void ShowCrashDialog(IClassicDesktopStyleApplicationLifetime desktop, Exception exception, string source)
    {
        var errorText = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {source}{Environment.NewLine}{Environment.NewLine}{exception}";

        Dispatcher.UIThread.Post(async () =>
        {
            if (_isShowingCrashDialog)
                return;

            _isShowingCrashDialog = true;

            try
            {
                var textBox = new TextBox
                {
                    Text = errorText,
                    IsReadOnly = true,
                    AcceptsReturn = true,
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                    MinHeight = 260
                };

                var errorScroll = new ScrollViewer
                {
                    Content = textBox,
                    MinHeight = 260
                };

                var copyButton = new Button
                {
                    Content = "Copy",
                    Width = 90
                };

                var closeButton = new Button
                {
                    Content = "Close",
                    Width = 90,
                    IsDefault = true
                };

                var buttonRow = new StackPanel
                {
                    Orientation = Avalonia.Layout.Orientation.Horizontal,
                    Spacing = 8,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                    Children = { copyButton, closeButton }
                };

                var content = new StackPanel
                {
                    Margin = new Thickness(16),
                    Spacing = 12,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = "The application hit an error. You can copy the full text below.",
                            TextWrapping = Avalonia.Media.TextWrapping.Wrap
                        },
                        errorScroll,
                        buttonRow
                    }
                };

                var window = new Window
                {
                    Title = "Application Error",
                    Width = 860,
                    Height = 520,
                    MinWidth = 720,
                    MinHeight = 420,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Content = content
                };

                copyButton.Click += async (_, _) =>
                {
                    var topLevel = TopLevel.GetTopLevel(window);
                    if (topLevel?.Clipboard is not null)
                        await topLevel.Clipboard.SetTextAsync(errorText);

                    textBox.Focus();
                    textBox.SelectAll();
                };

                closeButton.Click += (_, _) => window.Close();

                var owner = desktop.MainWindow;
                if (owner is not null)
                    await window.ShowDialog(owner);
                else
                    window.Show();
            }
            finally
            {
                _isShowingCrashDialog = false;
            }
        });
    }
}
