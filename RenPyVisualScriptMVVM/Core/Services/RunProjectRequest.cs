using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace RenPyVisualScriptMVVM.Core.Services
{
    internal sealed class RunProjectRequest
    {
        private readonly string _sdkPath;
        private readonly string _projectPath;

        public RunProjectRequest(string sdkPath, string projectPath)
        {
            _sdkPath = sdkPath ?? throw new ArgumentNullException(nameof(sdkPath));
            _projectPath = projectPath ?? throw new ArgumentNullException(nameof(projectPath));
        }

        public void Run()
        {
            if (!Directory.Exists(_sdkPath))
                throw new DirectoryNotFoundException($"Ren'Py SDK path not found: {_sdkPath}");

            if (!Directory.Exists(_projectPath))
                throw new DirectoryNotFoundException($"Project path not found: {_projectPath}");

            var scriptPath = Path.Combine(AppContext.BaseDirectory, "run_renpy_project.py");
            if (!File.Exists(scriptPath))
                throw new FileNotFoundException($"Не найден скрипт запуска проекта: {scriptPath}. " +
                                                $"Добавьте его в проект и включите Copy to Output Directory.");

            var pythonExe = ResolvePythonExe(_sdkPath);

            var psi = new ProcessStartInfo
            {
                FileName = pythonExe,
                UseShellExecute = false,
                CreateNoWindow = true,
                // Запускаем промежуточный python-скрипт из папки проекта,
                // чтобы все относительные пути и временные файлы создавались рядом с игрой.
                WorkingDirectory = _projectPath,
            };

            psi.ArgumentList.Add(scriptPath);
            psi.ArgumentList.Add("--sdk"); psi.ArgumentList.Add(_sdkPath);
            psi.ArgumentList.Add("--project"); psi.ArgumentList.Add(_projectPath);

            var p = Process.Start(psi);
            if (p is null)
                throw new InvalidOperationException("Не удалось запустить Ren'Py процесс.");
        }

        private static string ResolvePythonExe(string sdkPath)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var sdkPython = Path.Combine(sdkPath, "lib", "py3-windows-x86_64", "python.exe");
                if (File.Exists(sdkPython))
                    return sdkPython;

                return "python";
            }

            return "python3";
        }
    }
}
