using System.IO;
using System.Text.Json;
using RenPyVisualScriptMVVM.Modules.GraphEditor.Models;

namespace RenPyVisualScriptMVVM.Modules.GraphEditor.Services;

internal static class GraphViewStateStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public static GraphViewState Load(string? projectPath)
    {
        var filePath = GetFilePath(projectPath);
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            return new GraphViewState();

        try
        {
            var json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<GraphViewState>(json, JsonOptions) ?? new GraphViewState();
        }
        catch
        {
            return new GraphViewState();
        }
    }

    public static void Save(string? projectPath, GraphViewState state)
    {
        var filePath = GetFilePath(projectPath);
        if (string.IsNullOrWhiteSpace(filePath))
            return;

        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        var json = JsonSerializer.Serialize(state, JsonOptions);
        File.WriteAllText(filePath, json);
    }

    private static string? GetFilePath(string? projectPath)
    {
        if (string.IsNullOrWhiteSpace(projectPath))
            return null;

        return Path.Combine(projectPath, ".projectSettings", "graph-view-state.json");
    }
}
