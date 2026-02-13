using System;

namespace RenPyVisualScriptMVVM.Core.Services.Exceptions
{
    /// <summary>
    /// Выбрасывается при попытке создать проект с именем, которое уже занято.
    /// </summary>
    public sealed class ProjectAlreadyExistsException : Exception
    {
        public string ProjectName { get; }
        public string ProjectPath { get; }

        public ProjectAlreadyExistsException(string projectName, string projectPath)
            : base($"Проект с именем \"{projectName}\" уже существует. Выберите другое имя.")
        {
            ProjectName = projectName;
            ProjectPath = projectPath;
        }
    }
}
