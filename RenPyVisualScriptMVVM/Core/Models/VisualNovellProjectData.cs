using Avalonia.Media;
using RenPyVisualScriptMVVM.Modules.Projects.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RenPyVisualScriptMVVM.Core.Models
{
    public class VisualNovellProjectData
    {
        public string? ProjectsPath { get; set; }
        public string? Name { get; set; }
        public Tuple<int, int> Resolution { get; set; } = new Tuple<int, int>(1920, 1080);
        public Color color { get; set; } = new Color(255, 255, 255, 255);
        public string? Language { get; set; } = "russian";
    }
}
