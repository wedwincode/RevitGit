using RevitGit.Domain.Snapshots;

namespace RevitGit.Application.Abstractions
{
    public interface IFamilySnapshotProvider
    {
        FamilySnapshot CaptureSnapshot();
    }
}
