using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using RenPyVisualScriptMVVM.Infrastructure.StoryStorage.Logging;
using RenPyVisualScriptMVVM.Modules.Shell.ViewModels;

namespace RenPyVisualScriptMVVM.Modules.DatabaseLog.ViewModels;

public sealed class DatabaseLogWindowViewModel : BaseViewModel
{
    private readonly IDatabaseLogService _databaseLog;

    public ObservableCollection<DatabaseLogEntry> Entries { get; } = new();
    public IRelayCommand ClearCmd { get; }

    public DatabaseLogWindowViewModel(IDatabaseLogService databaseLog)
    {
        _databaseLog = databaseLog ?? throw new ArgumentNullException(nameof(databaseLog));
        foreach (var entry in _databaseLog.Entries)
            Entries.Add(entry);

        _databaseLog.EntryAdded += OnEntryAdded;
        ClearCmd = new RelayCommand(Clear);
    }

    public string GetAllText()
    {
        var builder = new StringBuilder();
        foreach (var entry in Entries)
            builder.AppendLine(entry.DisplayText);

        return builder.ToString();
    }

    private void Clear()
    {
        _databaseLog.Clear();
        Entries.Clear();
    }

    private void OnEntryAdded(DatabaseLogEntry entry)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            AddEntry(entry);
            return;
        }

        Dispatcher.UIThread.Post(() => AddEntry(entry));
    }

    private void AddEntry(DatabaseLogEntry entry)
    {
        Entries.Add(entry);
        while (Entries.Count > 1000)
            Entries.RemoveAt(0);
    }
}
