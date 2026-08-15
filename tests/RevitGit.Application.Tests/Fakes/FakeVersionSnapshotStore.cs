using System;
using System.Collections.Generic;
using RevitGit.Application.Abstractions;
using RevitGit.Application.Models;
using RevitGit.Domain.Identifiers;
using RevitGit.Domain.Snapshots;

namespace RevitGit.Application.Tests.Fakes
{
    internal sealed class FakeVersionSnapshotStore : IVersionSnapshotStore
    {
        private readonly Dictionary<VersionId, FamilySnapshot> _snapshots = new Dictionary<VersionId, FamilySnapshot>();
        public IList<VersionId> ReadVersionIds { get; } = new List<VersionId>();
        public Exception ReadException { get; set; }
        public void Seed(VersionId id, FamilySnapshot snapshot) => _snapshots[id] = snapshot;
        public FamilySnapshot ReadSnapshot(FamilyIdentity familyIdentity, VersionId versionId)
        {
            ReadVersionIds.Add(versionId);
            if (ReadException != null) throw ReadException;
            return _snapshots[versionId];
        }
    }
}
