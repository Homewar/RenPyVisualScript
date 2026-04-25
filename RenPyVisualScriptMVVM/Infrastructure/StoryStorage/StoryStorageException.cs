using System;

namespace RenPyVisualScriptMVVM.Infrastructure.StoryStorage;

public sealed class StoryStorageException : Exception
{
    public StoryStorageException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
