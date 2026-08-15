using System;
using RevitGit.Application.Abstractions;
using RevitGit.Application.Models;
using RevitGit.Domain.History;
using RevitGit.Domain.Identifiers;
using RevitGit.Infrastructure.FileSystem.History;
using RevitGit.Infrastructure.FileSystem.Repositories;
using RevitGit.Infrastructure.FileSystem.Serialization;
using RevitGit.Infrastructure.Git;

namespace RevitGit.Revit2021.Composition
{
    internal sealed class LazyFamilyHistoryStore : IHistoryRepository, IVersionContentStore
    {
        private readonly string _familyPath;
        private readonly FamilyRepositoryManager _repositoryManager;
        private readonly IFamilySnapshotProvider _snapshotProvider;
        private readonly SnapshotJsonSerializer _snapshotSerializer;
        private LibGit2VersionRepository _adapter;
        private byte[] _preparedSnapshot;

        public LazyFamilyHistoryStore(
            string familyPath,
            FamilyRepositoryManager repositoryManager,
            IFamilySnapshotProvider snapshotProvider,
            SnapshotJsonSerializer snapshotSerializer)
        {
            _familyPath = familyPath ?? throw new ArgumentNullException(nameof(familyPath));
            _repositoryManager = repositoryManager ?? throw new ArgumentNullException(nameof(repositoryManager));
            _snapshotProvider = snapshotProvider ?? throw new ArgumentNullException(nameof(snapshotProvider));
            _snapshotSerializer = snapshotSerializer ?? throw new ArgumentNullException(nameof(snapshotSerializer));
        }

        public bool Exists(FamilyIdentity familyIdentity)
        {
            return _repositoryManager.Exists(_familyPath) && EnsureAdapter().Exists(familyIdentity);
        }

        public FamilyHistory Load(FamilyIdentity familyIdentity)
        {
            if (!_repositoryManager.Exists(_familyPath))
            {
                return null;
            }

            return EnsureAdapter().Load(familyIdentity);
        }

        public void Save(FamilyIdentity familyIdentity, FamilyHistory history)
        {
            EnsureAdapter().Save(familyIdentity, history);
        }

        public void StoreCurrentVersion(FamilyIdentity familyIdentity, VersionId versionId)
        {
            _preparedSnapshot = _snapshotSerializer.Serialize(_snapshotProvider.CaptureSnapshot());
            try
            {
                EnsureAdapter().StoreCurrentVersion(familyIdentity, versionId);
            }
            finally
            {
                _preparedSnapshot = null;
            }
        }

        public void RestoreVersionContent(FamilyIdentity familyIdentity, VersionId versionId)
        {
            EnsureAdapter().RestoreVersionContent(familyIdentity, versionId);
        }

        public void Validate(FamilyIdentity familyIdentity)
        {
            EnsureAdapter().Load(familyIdentity);
        }

        private LibGit2VersionRepository EnsureAdapter()
        {
            if (_adapter != null)
            {
                return _adapter;
            }

            var repository = _repositoryManager.Exists(_familyPath)
                ? _repositoryManager.Open(_familyPath)
                : _repositoryManager.Initialize(_familyPath);
            var metadataRepository = new FileSystemHistoryRepository(_repositoryManager);
            _adapter = new LibGit2VersionRepository(
                repository.Paths.RepositoryDirectory,
                metadataRepository,
                GetPreparedSnapshot);
            return _adapter;
        }

        private byte[] GetPreparedSnapshot()
        {
            if (_preparedSnapshot == null)
            {
                throw new InvalidOperationException("Version snapshot has not been prepared.");
            }

            return _preparedSnapshot;
        }
    }
}
