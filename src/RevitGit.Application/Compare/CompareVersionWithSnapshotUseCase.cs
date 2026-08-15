using System;
using RevitGit.Application.Abstractions;
using RevitGit.Application.Diff;
using RevitGit.Application.Exceptions;
using RevitGit.Application.History;
using RevitGit.Application.Models;
using RevitGit.Domain.Identifiers;
using RevitGit.Domain.Snapshots;

namespace RevitGit.Application.Compare
{
    public sealed class CompareVersionWithSnapshotUseCase
    {
        private readonly IHistoryRepository _historyRepository;
        private readonly IVersionSnapshotStore _snapshotStore;
        private readonly FamilyDiffEngine _diffEngine;

        public CompareVersionWithSnapshotUseCase(IHistoryRepository historyRepository, IVersionSnapshotStore snapshotStore, FamilyDiffEngine diffEngine)
        {
            _historyRepository = historyRepository ?? throw new ArgumentNullException(nameof(historyRepository));
            _snapshotStore = snapshotStore ?? throw new ArgumentNullException(nameof(snapshotStore));
            _diffEngine = diffEngine ?? throw new ArgumentNullException(nameof(diffEngine));
        }

        public FamilyDiff Execute(FamilyIdentity familyIdentity, VersionId beforeVersionId, FamilySnapshot afterSnapshot)
        {
            if (familyIdentity == null) throw new ArgumentNullException(nameof(familyIdentity));
            if (afterSnapshot == null) throw new ArgumentNullException(nameof(afterSnapshot));
            var history = _historyRepository.LoadRequired(familyIdentity);
            history.GetVersion(beforeVersionId);
            FamilySnapshot before;
            try { before = _snapshotStore.ReadSnapshot(familyIdentity, beforeVersionId); }
            catch (ApplicationOperationException) { throw; }
            catch (Exception exception)
            {
                throw new ApplicationOperationException(ApplicationFailureStage.ReadVersionSnapshot,
                    "The selected version snapshot could not be read.", exception);
            }
            return _diffEngine.Compare(before, afterSnapshot);
        }
    }
}
