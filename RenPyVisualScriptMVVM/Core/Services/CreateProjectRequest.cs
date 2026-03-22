using Avalonia.Media;
using RenPyVisualScriptMVVM.Core.Models;
using RenPyVisualScriptMVVM.Core.Services.Interfaces;
using System;
using System.Diagnostics;
using System.IO;
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
            _sdkPath = settings.RenPySDKPath ?? string.Empty;
            _projectRoot = pf.RootFolder ?? throw new ArgumentNullException(nameof(pf.RootFolder));
            _projectName = pd.Name ?? throw new ArgumentNullException(nameof(pd.Name));
            _resolution = pd.Resolution ?? new Tuple<int, int>(1920, 1080);
            _color = pd.color;
            _language = pd.Language ?? "russian";
        }

        public async Task CreateAsync()
        {
            if (!RenPySdkPathResolver.IsValidSdkPath(_sdkPath))
                throw new InvalidOperationException("Ren'Py SDK path is not set or invalid.");

            var normalizedSdkPath = RenPySdkPathResolver.NormalizePath(_sdkPath);

            var scriptPath = Path.Combine(AppContext.BaseDirectory, "create_renpy_project.py");
            if (!File.Exists(scriptPath))
                throw new FileNotFoundException($"Не найден скрипт создания проекта: {scriptPath}. " +
                                                $"Добавьте его в проект и включите Copy to Output Directory.");

            var pythonExe = RenPySdkPathResolver.ResolvePythonExecutable(normalizedSdkPath);

            var width = _resolution.Item1;
            var height = _resolution.Item2;
            var accentHex = $"#{_color.R:X2}{_color.G:X2}{_color.B:X2}";

            Directory.CreateDirectory(_projectRoot);

            var psi = new ProcessStartInfo
            {
                FileName = pythonExe,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = AppContext.BaseDirectory,
            };

            psi.ArgumentList.Add(scriptPath);
            psi.ArgumentList.Add("--sdk"); psi.ArgumentList.Add(normalizedSdkPath);
            psi.ArgumentList.Add("--out"); psi.ArgumentList.Add(Path.GetFullPath(_projectRoot));
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
    }
}
