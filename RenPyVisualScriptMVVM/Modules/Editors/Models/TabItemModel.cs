using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RenPyVisualScriptMVVM.Modules.Editors.Models
{
    public sealed class TabItemModel : ObservableObject
    {
        private string _header;
        public string Header
        {
            get => _header;
            private set => SetProperty(ref _header, value);
        }

        private string _filePath;
        public string FilePath
        {
            get => _filePath;
            private set => SetProperty(ref _filePath, value);
        }

        private string _scriptText = string.Empty;
        public string ScriptText
        {
            get => _scriptText;
            set => SetProperty(ref _scriptText, value);
        }

        private int? _targetLine;
        public int? TargetLine
        {
            get => _targetLine;
            set => SetProperty(ref _targetLine, value);
        }

        private int _navigationRequestId;
        public int NavigationRequestId
        {
            get => _navigationRequestId;
            private set => SetProperty(ref _navigationRequestId, value);
        }

        private int _reloadRequestId;
        public int ReloadRequestId
        {
            get => _reloadRequestId;
            private set => SetProperty(ref _reloadRequestId, value);
        }

        private readonly HashSet<int> _breakpoints = new();

        private int _breakpointsVersion;
        public int BreakpointsVersion
        {
            get => _breakpointsVersion;
            private set => SetProperty(ref _breakpointsVersion, value);
        }

        private int? _activeBreakpointLine;
        public int? ActiveBreakpointLine
        {
            get => _activeBreakpointLine;
            private set => SetProperty(ref _activeBreakpointLine, value);
        }

        public IRelayCommand CloseCommand { get; }
        public IRelayCommand FileSavedCommand { get; }

        public TabItemModel(string header, string filePath, Action<TabItemModel> closeAction, Action<TabItemModel>? fileSavedAction = null)
        {
            _header = header;
            _filePath = filePath;
            CloseCommand = new RelayCommand(() => closeAction(this));
            FileSavedCommand = new RelayCommand(() => fileSavedAction?.Invoke(this));
        }

        public void UpdateFileIdentity(string header, string filePath)
        {
            Header = header;
            FilePath = filePath;
        }

        public void RequestNavigation(int? line)
        {
            TargetLine = line;
            NavigationRequestId++;
        }

        public void RequestReload(int? line = null)
        {
            if (line.HasValue)
                TargetLine = line;

            ReloadRequestId++;
            NavigationRequestId++;
        }

        public bool HasBreakpoint(int line) => _breakpoints.Contains(line);

        public IReadOnlyCollection<int> GetBreakpoints() => _breakpoints.OrderBy(line => line).ToArray();

        public void ToggleBreakpoint(int line)
        {
            if (line <= 0)
                return;

            if (_breakpoints.Contains(line))
            {
                _breakpoints.Remove(line);
                ActiveBreakpointLine = null;
            }
            else
            {
                _breakpoints.Clear();
                _breakpoints.Add(line);
                ActiveBreakpointLine = line;
            }

            BreakpointsVersion++;
        }
    }
}
