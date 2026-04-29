using System;

namespace RenPyVisualScriptMVVM.Infrastructure.StoryStorage.Logging;

public sealed class DatabaseLogEntry
{
    public DatabaseLogEntry(DateTimeOffset timestamp, string level, string operation, string message, string? exception = null)
    {
        Timestamp = timestamp;
        Level = level;
        Operation = operation;
        Message = message;
        Exception = exception;
    }

    public DateTimeOffset Timestamp { get; }
    public string Level { get; }
    public string Operation { get; }
    public string Message { get; }
    public string? Exception { get; }

    public string DisplayText => Exception is null
        ? $"[{Timestamp:HH:mm:ss.fff}] {Level,-5} {Operation}: {Message}"
        : $"[{Timestamp:HH:mm:ss.fff}] {Level,-5} {Operation}: {Message}{Environment.NewLine}{Exception}";
}
