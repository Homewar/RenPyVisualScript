using PropertyModels.ComponentModel;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RenPyVisualScriptMVVM.Core.Models
{
    public sealed class ProjectFiles
    { 
        public string ProjectName { get; }
        public string RootFolder  { get; }
        public string SettingPath => Path.Combine(RootFolder, "Settings");

        public ProjectFiles(string projectname, string rootfolder)
        {
            ProjectName = projectname;
            RootFolder = rootfolder;
        }
    }
}
