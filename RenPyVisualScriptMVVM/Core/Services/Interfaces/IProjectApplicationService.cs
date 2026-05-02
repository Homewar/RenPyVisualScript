using RenPyVisualScriptMVVM.Core.Models;
using System.Threading.Tasks;

namespace RenPyVisualScriptMVVM.Core.Services.Interfaces
{
    public interface IProjectApplicationService
    {
        Task<ProjectFiles> CreateNewAsync(VisualNovellProjectData data);
        ProjectFiles CopyExisting(string folderPath);
        ProjectFiles ImportExisting(string folderPath);
        ProjectFiles OpenExisting(string folderPath);
    }
}
