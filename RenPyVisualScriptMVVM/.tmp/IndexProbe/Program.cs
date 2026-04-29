using RenPyVisualScriptMVVM.Infrastructure.StoryStorage.Services;
using RenPyVisualScriptMVVM.Infrastructure.StoryStorage.Parsers;

var path = args.Length > 0 ? args[0] : Path.GetFullPath(Path.Combine("bin", "Debug", "net8.0", "Project", "TestWithBD"));
Console.WriteLine(path);
try
{
    var service = new StoryStorageService(new RenPyStoryParser());
    await service.RebuildProjectIndexAsync(path, Path.GetFileName(path));
    var snapshot = await service.ReadProjectStructureAsync(path);
    Console.WriteLine($"OK labels={snapshot.Labels.Count} links={snapshot.Links.Count}");
    foreach (var label in snapshot.Labels.Take(20)) Console.WriteLine($"label {label.Name} {label.FilePath}:{label.Line}-{label.EndLine}");
    foreach (var link in snapshot.Links.Take(20)) Console.WriteLine($"link {link.Kind} {link.Source}->{link.Target} {link.FileName}:{link.Line}");
}
catch (Exception ex)
{
    Console.WriteLine(ex.ToString());
    Environment.ExitCode = 1;
}