using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;

namespace RenPyVisualScriptMVVM.Modules.Editors.Models
{
    public sealed class TabItemModel : ObservableObject
    {
        public string Header { get; }
        public string FilePath { get; }

        private string _scriptText = string.Empty;
        public string ScriptText
        {
            get => _scriptText;
            set => SetProperty(ref _scriptText, value);
        }

        public IRelayCommand CloseCommand { get; }

        public TabItemModel(string header, string filePath, Action<TabItemModel> closeAction)
        {
            Header = header;
            FilePath = filePath;
            CloseCommand = new RelayCommand(() => closeAction(this));
        }
    }
}
