using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RenPyVisualScriptMVVM.Modules.Shell.ViewModels;

namespace RenPyVisualScriptMVVM.Modules.Shell.Services.Interfaces
{
    public interface IWindowService
    {
        Task<bool?> ShowDialogAsync<TVm>(TVm vm) where TVm : BaseViewModel;
        void ShowWindow<TVm>(TVm vm) where TVm : BaseViewModel;
    }
}
