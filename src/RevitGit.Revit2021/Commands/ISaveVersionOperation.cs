using RevitGit.Application.Models;

namespace RevitGit.Revit2021.Commands
{
    public interface ISaveVersionOperation
    {
        VersionSummary Execute(string comment);
    }
}
