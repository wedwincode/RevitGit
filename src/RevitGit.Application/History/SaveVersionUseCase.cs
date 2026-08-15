using System;
using RevitGit.Application.Abstractions;
using RevitGit.Application.Exceptions;
using RevitGit.Application.Models;

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
            var familyIdentity = _documentGateway.GetIdentity();
            var history = _historyRepository.LoadRequired(familyIdentity);

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

            try
            {
                _contentStore.StoreCurrentVersion(familyIdentity);
            }
            catch (Exception exception)
            {
                throw new ApplicationOperationException(
                    ApplicationFailureStage.StoreVersionContent,
                    "The version content could not be stored.",
                    exception);
            }

            var version = history.AddVersion(_clock.UtcNow, comment);
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
