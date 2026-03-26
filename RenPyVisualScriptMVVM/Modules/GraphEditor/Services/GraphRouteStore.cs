using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using RenPyVisualScriptMVVM.Modules.GraphEditor.Models;

namespace RenPyVisualScriptMVVM.Modules.GraphEditor.Services;

internal static class GraphRouteStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public static List<StoryRoute> Load(string? projectPath)
    {
        var filePath = GetFilePath(projectPath);
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            return new List<StoryRoute>();

        try
        {
            var json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<List<StoryRoute>>(json, JsonOptions) ?? new List<StoryRoute>();
        }
        catch
        {
            return new List<StoryRoute>();
        }
    }

    public static void Save(string? projectPath, IEnumerable<StoryRoute> routes)
    {
        var filePath = GetFilePath(projectPath);
        if (string.IsNullOrWhiteSpace(filePath))
            return;

        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        var normalizedRoutes = routes
            .Select(route => new StoryRoute
            {
                Name = route.Name,
                NodeTitles = route.NodeTitles.ToList()
            })
            .OrderBy(route => route.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var json = JsonSerializer.Serialize(normalizedRoutes, JsonOptions);
        File.WriteAllText(filePath, json);
    }

    private static string? GetFilePath(string? projectPath)
    {
        if (string.IsNullOrWhiteSpace(projectPath))
            return null;

        return Path.Combine(projectPath, ".projectSettings", "graph-routes.json");
    }
}
