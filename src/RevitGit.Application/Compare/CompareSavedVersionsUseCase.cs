using System;
using RevitGit.Application.Abstractions;
using RevitGit.Application.Diff;
using RevitGit.Application.Exceptions;
using RevitGit.Application.History;
using RevitGit.Application.Models;
using RevitGit.Domain.Identifiers;

namespace RevitGit.Application.Compare
{
    public sealed class CompareSavedVersionsUseCase
    {
        private readonly IHistoryRepository _historyRepository;
        private readonly IVersionSnapshotStore _snapshotStore;
        private readonly FamilyDiffEngine _diffEngine;

        public CompareSavedVersionsUseCase(IHistoryRepository historyRepository, IVersionSnapshotStore snapshotStore, FamilyDiffEngine diffEngine)
        {
            _historyRepository = historyRepository ?? throw new ArgumentNullException(nameof(historyRepository));
            _snapshotStore = snapshotStore ?? throw new ArgumentNullException(nameof(snapshotStore));
            _diffEngine = diffEngine ?? throw new ArgumentNullException(nameof(diffEngine));
        }

        public FamilyDiff Execute(FamilyIdentity familyIdentity, VersionId beforeVersionId, VersionId afterVersionId)
        {
            if (familyIdentity == null) throw new ArgumentNullException(nameof(familyIdentity));
            var history = _historyRepository.LoadRequired(familyIdentity);
            history.GetVersion(beforeVersionId);
            history.GetVersion(afterVersionId);
            return _diffEngine.Compare(Read(familyIdentity, beforeVersionId), Read(familyIdentity, afterVersionId));
        }

        private Domain.Snapshots.FamilySnapshot Read(FamilyIdentity identity, VersionId id)
        {
            try { return _snapshotStore.ReadSnapshot(identity, id); }
            catch (ApplicationOperationException) { throw; }
            catch (Exception exception)
            {
                throw new ApplicationOperationException(ApplicationFailureStage.ReadVersionSnapshot,
                    "The selected version snapshot could not be read.", exception);
            }
        }
    }
}
