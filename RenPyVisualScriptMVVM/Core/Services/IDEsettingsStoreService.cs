using RenPyVisualScriptMVVM.Core.Models;
using RenPyVisualScriptMVVM.Core.Services.Interfaces;
using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace RenPyVisualScriptMVVM.Core.Services
{
    internal class IDEsettingsStoreService : ISettingsIDE
    {
        private const string FileName = "ide-settings.json";

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        public async Task<IDESettings> LoadAsync()
        {
            var path = GetSettingsFilePath();

            if (!File.Exists(path))
                return CreateDefault();

            try
            {
                var json = await File.ReadAllTextAsync(path).ConfigureAwait(false);

                if (string.IsNullOrWhiteSpace(json))
                    return CreateDefault();

                var settings = JsonSerializer.Deserialize<IDESettings>(json, JsonOptions);
                if (settings is null)
                    return CreateDefault();

                // нормализация/защита от null
                settings.RenPySDKPath ??= string.Empty;

                Debug.WriteLine($"IDE settings loaded from: {path}");

                return settings;
            }
            catch
            {
                // файл битый/нечитаемый/невалидный JSON
                Debug.WriteLine($"Failed to load IDE settings from: {path}. Using default settings.");
                return CreateDefault();
            }
        }

        public async Task SaveAsync(IDESettings s)
        {
            if (s is null) throw new ArgumentNullException(nameof(s));

            var path = GetSettingsFilePath();
            var dir = Path.GetDirectoryName(path)!;

            Directory.CreateDirectory(dir);

            var json = JsonSerializer.Serialize(s, JsonOptions);
            await File.WriteAllTextAsync(path, json).ConfigureAwait(false);
        }

        private static IDESettings CreateDefault()
        {
            return new IDESettings
            {
                RenPySDKPath = string.Empty
            };
        }

        private static string GetSettingsFilePath()
        {
            // AppData/Roaming на Windows, ~/.config на Linux, Application Support на macOS
            var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var appDir = Path.Combine(baseDir, "RenPyVisualScriptMVVM");

            return Path.Combine(appDir, FileName);
        }

        public static bool IsValidSdkPath(string? sdkPath)
        {
            if (string.IsNullOrWhiteSpace(sdkPath)) return false;
            if (!Directory.Exists(sdkPath)) return false;
            if (!Directory.Exists(Path.Combine(sdkPath, "renpy"))) return false;
            if (!Directory.Exists(Path.Combine(sdkPath, "launcher"))) return false;

            var hasExe =
                File.Exists(Path.Combine(sdkPath, "renpy.exe")) ||
                File.Exists(Path.Combine(sdkPath, "renpy.bat")) ||
                File.Exists(Path.Combine(sdkPath, "renpy.sh"));

            return hasExe;
        }
    }
}