using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RenPyVisualScriptMVVM.Core.Services.Interfaces
{
    public interface ICloseRequest
    {
        event Action<bool?>? RequestClose;
    }
}
