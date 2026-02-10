using RenPyVisualScriptMVVM.Modules.Settings.Models;
using RenPyVisualScriptMVVM.Core.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace RenPyVisualScriptMVVM.Modules.Settings.Services
{
    public sealed class JsonSettingsService : ISettingsService
    {
        private readonly IProjectContext _ctx;
        private readonly string _fallbackFile;         

        public AppSettings Settings { get; private set; } = new();

        public JsonSettingsService(IProjectContext ctx)
        {
            _ctx = ctx;

            _fallbackFile = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "RenPyVisualScriptMVVM",
                "settings.json");
        }

        /* путь → либо рядом с проектом, либо fallback */
        private string GetFilePath()
        {
            if (!string.IsNullOrWhiteSpace(_ctx.ProjectPath))
                return Path.Combine(_ctx.ProjectPath, ".projectSettings", "settings.json");

            return _fallbackFile;
        }

        public void Load()
        {
            var file = GetFilePath();
            if (File.Exists(file))
                Settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(file)) ?? new();
        }

        public void Save()
        {
            var file = GetFilePath();
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);

            var json = JsonSerializer.Serialize(Settings,
                         new JsonSerializerOptions { WriteIndented = true });

            File.WriteAllText(file, json);
        }
    }

}
