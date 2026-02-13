using Avalonia.Media;
using RenPyVisualScriptMVVM.Core.Models;
using RenPyVisualScriptMVVM.Core.Services.Interfaces;
using RenPyVisualScriptMVVM.Core.Models;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace RenPyVisualScriptMVVM.Core.Services
{
    internal sealed class CreateProjectRequest : IProjectGenerator
    {
        private readonly string _sdkPath;
        private readonly string _projectRoot;
        private readonly string _projectName;
        private readonly Tuple<int, int> _resolution;
        private readonly Color _color;
        private readonly string _language;

        public CreateProjectRequest(IDESettings settings, ProjectFiles pf, VisualNovellProjectData pd)
        {
            _sdkPath = settings.RenPySDKPath ?? throw new ArgumentNullException(nameof(settings.RenPySDKPath));
            _projectRoot = pf.RootFolder ?? throw new ArgumentNullException(nameof(pf.RootFolder));
            _projectName = pd.Name ?? throw new ArgumentNullException(nameof(pd.Name));
            _resolution = pd.Resolution ?? new Tuple<int, int>(1920, 1080);
            _color = pd.color;
            _language = pd.Language ?? "russian";
        }

        public async Task CreateAsync()
        {
            // 1) Полный путь к скрипту
            // Если вы копируете скрипт в output:
            var scriptPath = Path.Combine(AppContext.BaseDirectory, "create_renpy_project.py");
            if (!File.Exists(scriptPath))
                throw new FileNotFoundException($"Не найден скрипт создания проекта: {scriptPath}. " +
                                                $"Добавьте его в проект и включите Copy to Output Directory.");

            // 2) Python executable
            // На Windows часто "python" есть не у всех. Лучше попытаться взять python из SDK, если он есть.
            var pythonExe = ResolvePythonExe(_sdkPath);

            var width = _resolution.Item1;
            var height = _resolution.Item2;
            var accentHex = $"#{_color.R:X2}{_color.G:X2}{_color.B:X2}";

            // гарантируем, что папка out существует
            Directory.CreateDirectory(_projectRoot);

            var psi = new ProcessStartInfo
            {
                FileName = pythonExe,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = AppContext.BaseDirectory, // чтобы относительный путь к скрипту работал
            };

            // Лучше через ArgumentList (корректно экранирует пробелы/кавычки)
            psi.ArgumentList.Add(scriptPath);
            psi.ArgumentList.Add("--sdk"); psi.ArgumentList.Add(_sdkPath);
            psi.ArgumentList.Add("--out"); psi.ArgumentList.Add(_projectRoot);
            psi.ArgumentList.Add("--name"); psi.ArgumentList.Add(_projectName);
            psi.ArgumentList.Add("--width"); psi.ArgumentList.Add(width.ToString());
            psi.ArgumentList.Add("--height"); psi.ArgumentList.Add(height.ToString());
            psi.ArgumentList.Add("--accent"); psi.ArgumentList.Add(accentHex);
            psi.ArgumentList.Add("--boring"); psi.ArgumentList.Add("#000000");

            if (!string.IsNullOrWhiteSpace(_language))
            {
                psi.ArgumentList.Add("--language");
                psi.ArgumentList.Add(_language);
            }

            using var p = Process.Start(psi) ?? throw new InvalidOperationException("Не удалось запустить процесс создания проекта.");

            var stdoutTask = p.StandardOutput.ReadToEndAsync();
            var stderrTask = p.StandardError.ReadToEndAsync();

            await p.WaitForExitAsync().ConfigureAwait(false);

            var stdout = await stdoutTask.ConfigureAwait(false);
            var stderr = await stderrTask.ConfigureAwait(false);

            if (p.ExitCode != 0)
                throw new Exception(
                    $"Project creation failed with exit code {p.ExitCode}.\n" +
                    $"Python: {pythonExe}\nScript: {scriptPath}\n\nSTDOUT:\n{stdout}\n\nSTDERR:\n{stderr}");
        }

        private static string ResolvePythonExe(string sdkPath)
        {
            // Windows: sdk/lib/py3-windows-x86_64/python.exe (как в вашем скрипте)
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var sdkPython = Path.Combine(sdkPath, "lib", "py3-windows-x86_64", "python.exe");
                if (File.Exists(sdkPython))
                    return sdkPython;

                // fallback
                return "python";
            }

            // Linux/macOS
            return "python3";
        }
    }
}