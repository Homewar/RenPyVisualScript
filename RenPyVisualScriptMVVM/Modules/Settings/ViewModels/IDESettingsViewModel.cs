using CommunityToolkit.Mvvm.Input;
using RenPyVisualScriptMVVM.Core.Models;
using RenPyVisualScriptMVVM.Core.Services.Interfaces;
using RenPyVisualScriptMVVM.Modules.Shell.ViewModels;

namespace RenPyVisualScriptMVVM.Modules.Settings.ViewModels
{
    public sealed class IDESettingsViewModel : BaseViewModel
    {
        private readonly ISettingsIDE? _settingsStore;

        public IDESettings Settings { get; }
        public IRelayCommand SaveCommand { get; }

        public IDESettingsViewModel(ISettingsIDE settingsStore, IDESettings settings)
        {
            _settingsStore = settingsStore;
            Settings = settings;
            SaveCommand = new RelayCommand(Save);
        }

        public IDESettingsViewModel()
        {
            Settings = new IDESettings();
            SaveCommand = new RelayCommand(() => { });
        }

        private void Save()
        {
            _settingsStore?.SaveAsync(Settings).GetAwaiter().GetResult();
        }
    }
}
