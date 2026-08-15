using RevitGit.Domain.Snapshots;

namespace RevitGit.Infrastructure.FileSystem.Serialization
{
    public interface IFamilySnapshotSerializer
    {
        byte[] Serialize(FamilySnapshot snapshot);

        FamilySnapshot Deserialize(byte[] content);
    }
}
