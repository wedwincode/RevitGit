using System;
using System.Collections.Generic;
using RevitGit.Application.Abstractions;
using RevitGit.Application.Models;
using RevitGit.Domain.Identifiers;
using RevitGit.Domain.Snapshots;

namespace RevitGit.Application.Tests.Fakes
{
    internal sealed class FakeVersionContentStore : IVersionContentStore, IPreparedRestoreContentStore
    {
        private readonly IList<string> _operations;

        public FakeVersionContentStore(IList<string> operations = null)
        {
            _operations = operations;
        }

        public bool StoreCalled { get; private set; }

        public VersionId StoredVersionId { get; private set; }

        public VersionId RestoredVersionId { get; private set; }

        public Exception StoreException { get; set; }

        public Exception RestoreException { get; set; }

        public void StoreCurrentVersion(FamilyIdentity familyIdentity, VersionId versionId)
        {
            StoreCalled = true;
            StoredVersionId = versionId;
            _operations?.Add("content-store");
            if (StoreException != null)
            {
                throw StoreException;
            }
        }

        public void RestoreVersionContent(FamilyIdentity familyIdentity, VersionId versionId)
        {
            RestoredVersionId = versionId;
            _operations?.Add("content-restore");
            if (RestoreException != null)
            {
                throw RestoreException;
            }
        }

        public PreparedRestoreContent PrepareRestoreContent(
            FamilyIdentity familyIdentity,
            VersionId sourceVersionId,
            VersionId expectedCurrentVersionId)
        {
            RestoredVersionId = sourceVersionId;
            _operations?.Add("content-prepare");
            if (RestoreException != null) throw RestoreException;
            return new PreparedRestoreContent(familyIdentity, sourceVersionId, expectedCurrentVersionId,
                "prepared.rfa", new FamilySnapshot("Family", "Category",
                    new FamilyParameterSnapshot[0], new FamilyTypeSnapshot[0], null), "checksum");
        }

        public void PublishPreparedRestore(FamilyIdentity familyIdentity, PreparedRestoreContent prepared)
        {
            _operations?.Add("content-publish");
        }

        public void RollbackPreparedRestore(FamilyIdentity familyIdentity, PreparedRestoreContent prepared)
        {
            _operations?.Add("content-rollback");
        }

        public void StorePreparedRestore(
            FamilyIdentity familyIdentity,
            PreparedRestoreContent prepared,
            VersionId restoredVersionId)
        {
            StoreCalled = true;
            StoredVersionId = restoredVersionId;
            _operations?.Add("content-store");
            if (StoreException != null) throw StoreException;
        }

        public void CleanupPreparedRestore(PreparedRestoreContent prepared)
        {
            _operations?.Add("content-cleanup");
        }
    }
}
