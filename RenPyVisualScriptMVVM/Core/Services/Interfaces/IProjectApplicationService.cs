using RenPyVisualScriptMVVM.Core.Models;
using RenPyVisualScriptMVVM.Core.Models;
using System.Threading.Tasks;

namespace RenPyVisualScriptMVVM.Core.Services.Interfaces
{
    public interface IProjectApplicationService
    {
        Task<ProjectFiles> CreateNewAsync(VisualNovellProjectData data);
        ProjectFiles OpenExisting(string folderPath);
    }
}
