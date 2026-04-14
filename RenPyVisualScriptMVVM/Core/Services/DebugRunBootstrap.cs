using System;
using System.IO;
using System.Text.RegularExpressions;

namespace RenPyVisualScriptMVVM.Core.Services;

internal static class DebugRunBootstrap
{
    private const string BootstrapRelativePath = "game/__rvs_run_from_here__.rpy";
    private static readonly Regex LabelNameRegex = new("^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled);

    public static void Prepare(string projectPath, string startLabel)
    {
        if (string.IsNullOrWhiteSpace(projectPath))
            throw new ArgumentException("Project path is required.", nameof(projectPath));

        if (string.IsNullOrWhiteSpace(startLabel) || !LabelNameRegex.IsMatch(startLabel))
            throw new InvalidOperationException($"Invalid Ren'Py label name: {startLabel}");

        var bootstrapPath = GetBootstrapPath(projectPath);
        Directory.CreateDirectory(Path.GetDirectoryName(bootstrapPath)!);

        var content =
            "init -1000 python:\n" +
            "    def _rvs_run_from_here():\n" +
            $"        renpy.jump(\"{startLabel}\")\n" +
            "    config.start_callbacks.append(_rvs_run_from_here)\n";

        File.WriteAllText(bootstrapPath, content);
    }

    public static void Cleanup(string projectPath)
    {
        if (string.IsNullOrWhiteSpace(projectPath))
            return;

        var bootstrapPath = GetBootstrapPath(projectPath);
        if (File.Exists(bootstrapPath))
            File.Delete(bootstrapPath);
    }

    private static string GetBootstrapPath(string projectPath)
    {
        var normalizedProjectPath = Path.GetFullPath(projectPath.Trim().Trim('"'));
        return Path.Combine(normalizedProjectPath, BootstrapRelativePath);
    }
}
