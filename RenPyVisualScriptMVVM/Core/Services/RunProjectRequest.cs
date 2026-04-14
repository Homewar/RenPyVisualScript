using System;
using System.Diagnostics;
using System.IO;

namespace RenPyVisualScriptMVVM.Core.Services
{
    internal sealed class RunProjectRequest
    {
        private readonly string _sdkPath;
        private readonly string _projectPath;

        public RunProjectRequest(string sdkPath, string projectPath)
        {
            _sdkPath = sdkPath ?? string.Empty;
            _projectPath = projectPath ?? throw new ArgumentNullException(nameof(projectPath));
        }

        public void Run(string? startLabel = null)
        {
            if (!RenPySdkPathResolver.IsValidSdkPath(_sdkPath))
                throw new InvalidOperationException("Ren'Py SDK path is not set or invalid.");

            var normalizedSdkPath = RenPySdkPathResolver.NormalizePath(_sdkPath);
            var normalizedProjectPath = Path.GetFullPath(_projectPath.Trim().Trim('"'));

            if (!Directory.Exists(normalizedProjectPath))
                throw new DirectoryNotFoundException($"Project path not found: {normalizedProjectPath}");

            DebugRunBootstrap.Cleanup(normalizedProjectPath);
            if (!string.IsNullOrWhiteSpace(startLabel))
                DebugRunBootstrap.Prepare(normalizedProjectPath, startLabel);

            var scriptPath = Path.Combine(AppContext.BaseDirectory, "run_renpy_project.py");
            if (!File.Exists(scriptPath))
                throw new FileNotFoundException($"Не найден скрипт запуска проекта: {scriptPath}. " +
                                                $"Добавьте его в проект и включите Copy to Output Directory.");

            var pythonExe = RenPySdkPathResolver.ResolvePythonExecutable(normalizedSdkPath);

            var psi = new ProcessStartInfo
            {
                FileName = pythonExe,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = normalizedProjectPath,
            };

            psi.ArgumentList.Add(scriptPath);
            psi.ArgumentList.Add("--sdk"); psi.ArgumentList.Add(normalizedSdkPath);
            psi.ArgumentList.Add("--project"); psi.ArgumentList.Add(normalizedProjectPath);

            var p = Process.Start(psi);
            if (p is null)
                throw new InvalidOperationException("Не удалось запустить Ren'Py процесс.");
        }
    }
}
