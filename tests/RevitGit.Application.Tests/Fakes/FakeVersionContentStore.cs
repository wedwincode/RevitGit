using System;
using System.Collections.Generic;
using RevitGit.Application.Abstractions;
using RevitGit.Application.Models;
using RevitGit.Domain.Identifiers;

namespace RevitGit.Application.Tests.Fakes
{
    internal sealed class FakeVersionContentStore : IVersionContentStore
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
    }
}
