using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RenPyVisualScriptMVVM.Core.Models
{
    public sealed class Character
    {
        public string Name { get; }
        public string Color { get; }
        public string InGameName { get; }
        public string FilePath { get; }
        public int Line { get; }

        public Character(string name, string color, string inGameName, string filePath, int line) =>
            (Name, Color, InGameName, FilePath, Line) = (name, color, inGameName, filePath, line);
    }
}
