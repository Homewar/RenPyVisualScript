using CommunityToolkit.Mvvm.ComponentModel;

namespace RenPyVisualScriptMVVM.Core.Models
{
    public partial class IDESettings : ObservableObject
    {
        [ObservableProperty]
        private string? renPySDKPath = string.Empty;

        [ObservableProperty]
        private bool showSystemResources = true;
    }
}
