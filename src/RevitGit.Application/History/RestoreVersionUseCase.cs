using System;
using RevitGit.Application.Abstractions;
using RevitGit.Application.Exceptions;
using RevitGit.Application.Models;
using RevitGit.Domain.Identifiers;

namespace RevitGit.Application.History
{
    public sealed class RestoreVersionUseCase
    {
        private readonly IHistoryRepository _historyRepository;
        private readonly IFamilyDocumentGateway _documentGateway;
        private readonly IVersionContentStore _contentStore;
        private readonly IClock _clock;

        public RestoreVersionUseCase(
            IHistoryRepository historyRepository,
            IFamilyDocumentGateway documentGateway,
            IVersionContentStore contentStore,
            IClock clock)
        {
            _historyRepository = historyRepository;
            _documentGateway = documentGateway;
            _contentStore = contentStore;
            _clock = clock;
        }

        public VersionSummary Execute(VersionId sourceVersionId, string comment = null)
        {
            var preparedStore = _contentStore as IPreparedRestoreContentStore;
            if (preparedStore != null)
            {
                var prepared = Prepare(sourceVersionId);
                try
                {
                    preparedStore.PublishPreparedRestore(prepared.FamilyIdentity, prepared);
                    return Finalize(prepared, comment);
                }
                finally
                {
                    preparedStore.CleanupPreparedRestore(prepared);
                }
            }

            var familyIdentity = _documentGateway.GetIdentity();
            var history = _historyRepository.LoadRequired(familyIdentity);
            history.GetVersion(sourceVersionId);
            var restoredVersionId = VersionId.New();

            try
            {
                _contentStore.RestoreVersionContent(familyIdentity, sourceVersionId);
            }
            catch (Exception exception)
            {
                throw new ApplicationOperationException(
                    ApplicationFailureStage.RestoreVersionContent,
                    "The selected version content could not be restored.",
                    exception);
            }

            try
            {
                _contentStore.StoreCurrentVersion(familyIdentity, restoredVersionId);
            }
            catch (Exception exception)
            {
                throw new ApplicationOperationException(
                    ApplicationFailureStage.StoreVersionContent,
                    "The restored version content could not be stored.",
                    exception);
            }

            var version = history.AddRestoredVersion(
                restoredVersionId,
                sourceVersionId,
                _clock.UtcNow,
                comment);
            _historyRepository.SaveUpdated(familyIdentity, history);

            return new VersionSummary(
                version.Id,
                version.ParentVersionId,
                version.CreatedAt,
                version.Comment,
                version.RestoredFromVersionId,
                true);
        }

        public PreparedRestoreContent Prepare(VersionId sourceVersionId)
        {
            var preparedStore = RequirePreparedStore();
            var familyIdentity = _documentGateway.GetIdentity();
            var history = _historyRepository.LoadRequired(familyIdentity);
            history.GetVersion(sourceVersionId);
            var current = history.GetVariant(history.CurrentVariantId).CurrentVersionId;
            if (current.Equals(sourceVersionId))
                throw new InvalidOperationException("The selected version is already current.");
            try
            {
                return preparedStore.PrepareRestoreContent(familyIdentity, sourceVersionId, current);
            }
            catch (Exception exception)
            {
                throw new ApplicationOperationException(ApplicationFailureStage.RestoreVersionContent,
                    "The selected version content could not be prepared.", exception);
            }
        }

        public VersionSummary Finalize(PreparedRestoreContent prepared, string comment = null)
        {
            if (prepared == null) throw new ArgumentNullException(nameof(prepared));
            var preparedStore = RequirePreparedStore();
            // The Revit document used during Prepare is intentionally closed before Finalize.
            // The immutable prepared identity is the neutral concurrency boundary from here on.
            var familyIdentity = prepared.FamilyIdentity;
            var history = _historyRepository.LoadRequired(familyIdentity);
            history.GetVersion(prepared.SourceVersionId);
            var current = history.GetVariant(history.CurrentVariantId).CurrentVersionId;
            if (!current.Equals(prepared.ExpectedCurrentVersionId))
                throw new InvalidOperationException("The current version changed during restore.");
            var restoredVersionId = VersionId.New();
            try
            {
                preparedStore.StorePreparedRestore(familyIdentity, prepared, restoredVersionId);
            }
            catch (Exception exception)
            {
                throw new ApplicationOperationException(ApplicationFailureStage.StoreVersionContent,
                    "The restored version content could not be stored.", exception);
            }
            var version = history.AddRestoredVersion(restoredVersionId, prepared.SourceVersionId,
                _clock.UtcNow, comment);
            _historyRepository.SaveUpdated(familyIdentity, history);
            return new VersionSummary(version.Id, version.ParentVersionId, version.CreatedAt,
                version.Comment, version.RestoredFromVersionId, true);
        }

        public void Publish(PreparedRestoreContent prepared)
        {
            if (prepared == null) throw new ArgumentNullException(nameof(prepared));
            RequirePreparedStore().PublishPreparedRestore(prepared.FamilyIdentity, prepared);
        }

        public void Rollback(PreparedRestoreContent prepared)
        {
            if (prepared == null) throw new ArgumentNullException(nameof(prepared));
            RequirePreparedStore().RollbackPreparedRestore(prepared.FamilyIdentity, prepared);
        }

        public void Cleanup(PreparedRestoreContent prepared)
        {
            if (prepared != null) RequirePreparedStore().CleanupPreparedRestore(prepared);
        }

        private IPreparedRestoreContentStore RequirePreparedStore()
        {
            var preparedStore = _contentStore as IPreparedRestoreContentStore;
            if (preparedStore == null)
                throw new InvalidOperationException("Prepared restore is not supported by this content store.");
            return preparedStore;
        }

    }
}
