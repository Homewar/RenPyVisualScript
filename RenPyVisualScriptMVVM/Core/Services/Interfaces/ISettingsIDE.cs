using RenPyVisualScriptMVVM.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RenPyVisualScriptMVVM.Core.Services.Interfaces
{
    public interface ISettingsIDE
    {
        Task<IDESettings> LoadAsync();
        Task SaveAsync(IDESettings s);
    }


}
