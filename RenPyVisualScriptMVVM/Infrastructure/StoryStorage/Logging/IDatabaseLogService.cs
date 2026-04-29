using System;
using System.Collections.Generic;

namespace RenPyVisualScriptMVVM.Infrastructure.StoryStorage.Logging;

public interface IDatabaseLogService
{
    event Action<DatabaseLogEntry>? EntryAdded;
    IReadOnlyList<DatabaseLogEntry> Entries { get; }
    void Info(string operation, string message);
    void Warning(string operation, string message);
    void Error(string operation, string message, Exception exception);
    void Clear();
}
