using System;
using System.Collections.Generic;
using System.Linq;

namespace RenPyVisualScriptMVVM.Infrastructure.StoryStorage.Logging;

public sealed class DatabaseLogService : IDatabaseLogService
{
    private const int MaxEntries = 1000;
    private readonly object _gate = new();
    private readonly List<DatabaseLogEntry> _entries = new();

    public event Action<DatabaseLogEntry>? EntryAdded;

    public IReadOnlyList<DatabaseLogEntry> Entries
    {
        get
        {
            lock (_gate)
                return _entries.ToList();
        }
    }

    public void Info(string operation, string message) => Add("INFO", operation, message);

    public void Warning(string operation, string message) => Add("WARN", operation, message);

    public void Error(string operation, string message, Exception exception)
    {
        Add("ERROR", operation, message, exception.ToString());
    }

    public void Clear()
    {
        lock (_gate)
            _entries.Clear();
    }

    private void Add(string level, string operation, string message, string? exception = null)
    {
        var entry = new DatabaseLogEntry(DateTimeOffset.Now, level, operation, message, exception);
        lock (_gate)
        {
            _entries.Add(entry);
            if (_entries.Count > MaxEntries)
                _entries.RemoveRange(0, _entries.Count - MaxEntries);
        }

        EntryAdded?.Invoke(entry);
    }
}
