using System;
using System.IO;
using System.Text.RegularExpressions;

namespace RenPyVisualScriptMVVM.Core.Services;

internal static class DebugRunBootstrap
{
    private const string BootstrapRelativePath = "game/__rvs_run_from_here__.rpy";
    private const string BootstrapBaseName = "__rvs_run_from_here__";
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
            "label before_main_menu:\n" +
            "    python hide:\n" +
            $"        renpy.jump_out_of_context(\"{startLabel}\")\n";

        File.WriteAllText(bootstrapPath, content);
    }

    public static void Cleanup(string projectPath)
    {
        if (string.IsNullOrWhiteSpace(projectPath))
            return;

        var normalizedProjectPath = Path.GetFullPath(projectPath.Trim().Trim('"'));
        var bootstrapPath = GetBootstrapPath(normalizedProjectPath);
        DeleteIfExists(bootstrapPath);

        var bootstrapDirectory = Path.GetDirectoryName(bootstrapPath);
        if (string.IsNullOrWhiteSpace(bootstrapDirectory) || !Directory.Exists(bootstrapDirectory))
            return;

        foreach (var file in Directory.EnumerateFiles(bootstrapDirectory, $"{BootstrapBaseName}.*", SearchOption.AllDirectories))
        {
            var extension = Path.GetExtension(file);
            if (extension.Equals(".rpy", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".rpyc", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".rpym", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".rpymc", StringComparison.OrdinalIgnoreCase))
            {
                DeleteIfExists(file);
            }
        }
    }

    private static string GetBootstrapPath(string projectPath)
    {
        var normalizedProjectPath = Path.GetFullPath(projectPath.Trim().Trim('"'));
        return Path.Combine(normalizedProjectPath, BootstrapRelativePath);
    }

    private static void DeleteIfExists(string filePath)
    {
        if (File.Exists(filePath))
            File.Delete(filePath);
    }
}
