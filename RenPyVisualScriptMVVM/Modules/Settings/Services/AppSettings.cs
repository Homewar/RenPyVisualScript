using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RenPyVisualScriptMVVM.Modules.Settings.Services
{
    public class ProjectInfo
    {
        public string Name { get; set; } = "";
        public string FolderPath { get; set; } = "";
    }
    public sealed class AppSettings
    {
        public string? ProjectName { get; set; }
        public string? ProjectPath { get; set; }
    }
}
