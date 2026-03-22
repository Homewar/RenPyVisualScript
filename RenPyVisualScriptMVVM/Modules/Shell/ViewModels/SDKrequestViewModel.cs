using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.Input;
using RenPyVisualScriptMVVM.Core.Models;
using RenPyVisualScriptMVVM.Core.Services;
using RenPyVisualScriptMVVM.Core.Services.Interfaces;
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
    internal partial class SDKrequestViewModel : BaseViewModel
    {
        private readonly ISettingsIDE IDEsettingsStoreService;
        public event Action? RequestClose;

        private readonly bool _isFirstRun; 
        public bool CanCancel => !_isFirstRun;

        public SDKrequestViewModel(ISettingsIDE ideSettingsStoreService, bool isFirstRun)
        {
            IDEsettingsStoreService = ideSettingsStoreService;
            _isFirstRun = isFirstRun;
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

                // 1) получаем активное окно
                var lifetime = Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
                var window = lifetime?.Windows.FirstOrDefault(w => w.IsActive) ?? lifetime?.MainWindow;
                if (window is null)
                {
                    ErrorText = "Не удалось открыть диалог выбора папки (окно не найдено).";
                    return;
                }

                var sp = window.StorageProvider;
                if (sp is null)
                {
                    ErrorText = "StorageProvider недоступен (обнови Avalonia до версии с StorageProvider).";
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
                    ErrorText = "Выбранная папка не имеет локального пути.";
                    return;
                }

                // 3) валидируем, что это действительно SDK
                if (!IsValidRenPySdk(path, out var reason))
                {
                    ErrorText = reason;
                    return;
                }

                Sdkpath = path;
            }
            catch (Exception ex)
            {
                ErrorText = "Ошибка выбора папки: " + ex.Message;
            }
        }

        [RelayCommand]
        public void CancelCommand()
        {
            RequestClose?.Invoke();
        }

        [RelayCommand]
        public async Task SaveCommand()
        {
            var s = await IDEsettingsStoreService.LoadAsync();
            s.RenPySDKPath = Sdkpath;
            await IDEsettingsStoreService.SaveAsync(s);
            RequestClose?.Invoke();
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