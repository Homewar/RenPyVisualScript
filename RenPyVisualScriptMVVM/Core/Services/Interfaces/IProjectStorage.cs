using RenPyVisualScriptMVVM.Modules.Projects.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RenPyVisualScriptMVVM.Core.Services.Interfaces
{
    public interface IProjectStorage
    {
        ProjectFiles Create(string ProjectName);
        ProjectFiles Load(string rootFolder);
    }
}
