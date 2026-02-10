using System;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using RenPyVisualScriptMVVM.Core.ViewModels;

namespace RenPyVisualScriptMVVM
{
    public class ViewLocator : IDataTemplate
    {
        public Control? Build(object? param)
        {
            if (param is null)
                return null;

            var vmType = param.GetType();
            var viewTypeName = vmType.FullName!
                .Replace(".ViewModels.", ".Views.", StringComparison.Ordinal)
                .Replace("ViewModel", "View", StringComparison.Ordinal);

            // Use the same assembly as the VM to resolve the view type reliably.
            var viewType = vmType.Assembly.GetType(viewTypeName);

            if (viewType != null)
                return (Control)Activator.CreateInstance(viewType)!;

            return new TextBlock { Text = "Not Found: " + viewTypeName };
        }

        public bool Match(object? data) => data is BaseViewModel;
    }
}
