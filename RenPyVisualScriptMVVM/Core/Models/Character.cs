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

        public Character(string name, string color, string inGameName) =>
            (Name, Color, InGameName) = (name, color, inGameName);
    }
}
