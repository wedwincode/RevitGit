using System;
using RevitGit.Application.Abstractions;
using RevitGit.Application.Exceptions;
using RevitGit.Application.Models;
using RevitGit.Domain.History;
using RevitGit.Domain.Identifiers;
using HistoryVersion = RevitGit.Domain.History.Version;

namespace RevitGit.Application.History
{
    public sealed class SaveVersionUseCase
    {
        private readonly IHistoryRepository _historyRepository;
        private readonly IFamilyDocumentGateway _documentGateway;
        private readonly IVersionContentStore _contentStore;
        private readonly IClock _clock;

        public SaveVersionUseCase(
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

        public VersionSummary Execute(string comment)
        {
            return Execute(comment, "Main");
        }

        public VersionSummary Execute(string comment, string initialVariantName)
        {
            var familyIdentity = _documentGateway.GetIdentity();

            try
            {
                _documentGateway.Save();
            }
            catch (Exception exception)
            {
                throw new ApplicationOperationException(
                    ApplicationFailureStage.SaveDocument,
                    "The family document could not be saved.",
                    exception);
            }

            var history = _historyRepository.Load(familyIdentity);
            var isInitialVersion = history == null;
            var createdAt = _clock.UtcNow;
            var versionId = VersionId.New();
            HistoryVersion version = null;
            if (isInitialVersion)
            {
                history = FamilyHistory.Create(initialVariantName, createdAt, comment);
                version = history.GetVersion(history.GetVariant(history.CurrentVariantId).CurrentVersionId);
                versionId = version.Id;
            }

            try
            {
                _contentStore.StoreCurrentVersion(familyIdentity, versionId);
            }
            catch (Exception exception)
            {
                throw new ApplicationOperationException(
                    ApplicationFailureStage.StoreVersionContent,
                    "The version content could not be stored.",
                    exception);
            }

            if (!isInitialVersion)
            {
                version = history.AddVersion(versionId, createdAt, comment);
            }

            _historyRepository.SaveUpdated(familyIdentity, history);

            return new VersionSummary(
                version.Id,
                version.ParentVersionId,
                version.CreatedAt,
                version.Comment,
                version.RestoredFromVersionId,
                true);
        }

    }
}
