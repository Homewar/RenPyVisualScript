using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace RenPyVisualScriptMVVM.Core.Services.Interfaces
{

    public interface IProjectContext : INotifyPropertyChanged
    {
        string? ProjectName { get; set; }
        string? ProjectPath { get; set; }
    }

    public sealed class ProjectContext : ObservableObject, IProjectContext
    {
        private string? _projectName;
        public string? ProjectName
        {
            get => _projectName;
            set => SetProperty(ref _projectName, value);
        }

        private string? _projectPath;
        public string? ProjectPath
        {
            get => _projectPath;
            set => SetProperty(ref _projectPath, value);
        }
    }
}