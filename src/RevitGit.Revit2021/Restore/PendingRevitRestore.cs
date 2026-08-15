using RevitGit.Application.History;
using RevitGit.Application.Models;

namespace RevitGit.Revit2021.Restore
{
    internal sealed class PendingRevitRestore
    {
        public PendingRevitRestore(
            string originalPath,
            RestoreVersionUseCase useCase,
            PreparedRestoreContent prepared,
            VersionSummary version)
        {
            OriginalPath = originalPath;
            UseCase = useCase;
            Prepared = prepared;
            Version = version;
        }

        public string OriginalPath { get; }
        public RestoreVersionUseCase UseCase { get; }
        public PreparedRestoreContent Prepared { get; }
        public VersionSummary Version { get; }
    }
}
