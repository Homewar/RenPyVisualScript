using RenPyVisualScriptMVVM.Modules.Settings.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RenPyVisualScriptMVVM.Core.Services.Interfaces
{
    public interface ISettingsService
    {
        AppSettings Settings { get; }
        void Load();
        void Save();
    }
}

