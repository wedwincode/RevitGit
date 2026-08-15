using System;
using RevitGit.Application.Abstractions;
using RevitGit.Application.Models;
using RevitGit.Domain.Identifiers;
using RevitGit.Domain.Snapshots;
using RevitGit.Infrastructure.FileSystem.Repositories;
using RevitGit.Infrastructure.FileSystem.Serialization;

namespace RevitGit.Infrastructure.FileSystem.Versions
{
    public sealed class FileSystemVersionContentStore : IVersionContentStore
    {
        private readonly IFamilySnapshotProvider _snapshotProvider;
        private readonly FileSystemVersionStore _versionStore;

        public FileSystemVersionContentStore(
            FamilyRepositoryManager repositoryManager,
            IFamilySnapshotProvider snapshotProvider,
            IFamilySnapshotSerializer snapshotSerializer)
        {
            _snapshotProvider = snapshotProvider ?? throw new ArgumentNullException(nameof(snapshotProvider));
            _versionStore = new FileSystemVersionStore(repositoryManager, snapshotSerializer);
        }

        public void StoreCurrentVersion(FamilyIdentity familyIdentity, VersionId versionId)
        {
            if (familyIdentity == null)
            {
                throw new ArgumentNullException(nameof(familyIdentity));
            }

            _versionStore.SaveVersion(
                familyIdentity.Value,
                versionId,
                _snapshotProvider.CaptureSnapshot());
        }

        public void RestoreVersionContent(FamilyIdentity familyIdentity, VersionId versionId)
        {
            if (familyIdentity == null)
            {
                throw new ArgumentNullException(nameof(familyIdentity));
            }

            _versionStore.RestoreVersion(familyIdentity.Value, versionId);
        }

        public FamilySnapshot LoadSnapshot(FamilyIdentity familyIdentity, VersionId versionId)
        {
            if (familyIdentity == null)
            {
                throw new ArgumentNullException(nameof(familyIdentity));
            }

            return _versionStore.LoadSnapshot(familyIdentity.Value, versionId);
        }
    }
}
