using RenPyVisualScriptMVVM.Infrastructure.StoryStorage.Parsers;

namespace RenPyVisualScriptMVVM.Infrastructure.StoryStorage.Interfaces;

public interface IRenPyStoryParser
{
    ParsedStoryProject ParseProject(string projectPath, string? projectName = null);
}
