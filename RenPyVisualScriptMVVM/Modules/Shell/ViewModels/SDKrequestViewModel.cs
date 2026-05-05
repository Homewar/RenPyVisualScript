using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.Input;
using RenPyVisualScriptMVVM.Core.Models;
using RenPyVisualScriptMVVM.Core.Services;
using RenPyVisualScriptMVVM.Core.Services.Interfaces;
using RenPyVisualScriptMVVM.Modules.Shell.Services.Interfaces;
using RenPyVisualScriptMVVM.Modules.Shell.ViewModels;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace RenPyVisualScriptMVVM.Modules.Shell.ViewModels
{
    internal partial class SDKrequestViewModel : BaseViewModel, ICloseRequest
    {
        private readonly ISettingsIDE _ideSettingsStoreService;
        private readonly IDESettings _settings;
        public event Action<bool?>? RequestClose;

        private readonly bool _isFirstRun; 
        public bool CanCancel => !_isFirstRun;

        public SDKrequestViewModel(ISettingsIDE ideSettingsStoreService, IDESettings settings, bool isFirstRun)
        {
            _ideSettingsStoreService = ideSettingsStoreService;
            _settings = settings;
            _isFirstRun = isFirstRun;
            Sdkpath = settings.RenPySDKPath ?? string.Empty;
        }

        private string _sdkPath = string.Empty;
        public string Sdkpath
        {
            get => _sdkPath;
            set => SetProperty(ref _sdkPath, value);
        }

        private string? _errorMessage;
        public string? ErrorText
        {
            get => _errorMessage;
            set => SetProperty(ref _errorMessage, value);
        }

        private bool _hasError;
        public bool HasError
        {
            get => _hasError;
            set => SetProperty(ref _hasError, value);
        }


        [RelayCommand]
        public async Task BrowseAsync()
        {
            try
            {
                ErrorText = null;
                HasError = false;

                // 1) получаем активное окно
                var lifetime = Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
                var window = lifetime?.Windows.FirstOrDefault(w => w.IsActive) ?? lifetime?.MainWindow;
                if (window is null)
                {
                    SetError("Не удалось открыть диалог выбора папки (окно не найдено).");
                    return;
                }

                var sp = window.StorageProvider;
                if (sp is null)
                {
                    SetError("StorageProvider недоступен (обнови Avalonia до версии с StorageProvider).");
                    return;
                }

                // 2) выбираем папку SDK
                var folders = await sp.OpenFolderPickerAsync(new FolderPickerOpenOptions
                {
                    Title = "Выберите папку Ren'Py SDK",
                    AllowMultiple = false
                });

                var folder = folders?.FirstOrDefault();
                if (folder is null)
                    return; // пользователь отменил

                var path = folder.TryGetLocalPath();
                if (string.IsNullOrWhiteSpace(path))
                {
                    SetError("Выбранная папка не имеет локального пути.");
                    return;
                }

                // 3) валидируем, что это действительно SDK
                if (!IsValidRenPySdk(path, out var reason))
                {
                    SetError(reason);
                    return;
                }

                Sdkpath = path;
                ClearError();
            }
            catch (Exception ex)
            {
                SetError("Ошибка выбора папки: " + ex.Message);
            }
        }

        [RelayCommand]
        public void CancelCommand()
        {
            RequestClose?.Invoke(false);
        }

        [RelayCommand]
        public async Task SaveCommand()
        {
            if (!IsValidRenPySdk(Sdkpath, out var reason))
            {
                SetError(reason);
                return;
            }

            _settings.RenPySDKPath = RenPySdkPathResolver.NormalizePath(Sdkpath);
            await _ideSettingsStoreService.SaveAsync(_settings);
            ClearError();
            RequestClose?.Invoke(true);
        }

        private void SetError(string message)
        {
            ErrorText = message;
            HasError = true;
        }

        private void ClearError()
        {
            ErrorText = null;
            HasError = false;
        }

        private static bool IsValidRenPySdk(string path, out string reason)
        {
            reason = null!;

            if (!Directory.Exists(path))
            {
                reason = "Папка не существует.";
                return false;
            }

            if (!RenPySdkPathResolver.IsValidSdkPath(path))
            {
                reason = "Это не похоже на Ren'Py SDK: не найдены обязательные папки или файлы запуска.";
                return false;
            }

            return true;
        }
    }
}
