using System;
using RevitGit.Application.Abstractions;
using RevitGit.Application.Exceptions;
using RevitGit.Application.Models;
using RevitGit.Domain.History;

namespace RevitGit.Application.History
{
    public sealed class InitializeHistoryUseCase
    {
        private readonly IHistoryRepository _historyRepository;
        private readonly IFamilyDocumentGateway _documentGateway;
        private readonly IVersionContentStore _contentStore;
        private readonly IClock _clock;

        public InitializeHistoryUseCase(
            IHistoryRepository historyRepository,
            IFamilyDocumentGateway documentGateway,
            IClock clock)
        {
            _historyRepository = historyRepository;
            _documentGateway = documentGateway;
            _clock = clock;
        }

        public InitializeHistoryUseCase(
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

        public InitializeHistoryResult Execute(string mainVariantName)
        {
            var familyIdentity = _documentGateway.GetIdentity();
            if (_historyRepository.Exists(familyIdentity))
            {
                return new InitializeHistoryResult(false);
            }

            var history = FamilyHistory.Create(mainVariantName, _clock.UtcNow, null);
            if (_contentStore != null)
            {
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
                    var initialVersionId = history.GetVariant(history.CurrentVariantId).CurrentVersionId;
                    _contentStore.StoreCurrentVersion(familyIdentity, initialVersionId);
                }
                catch (Exception exception)
                {
                    throw new ApplicationOperationException(
                        ApplicationFailureStage.StoreVersionContent,
                        "The initial version content could not be stored.",
                        exception);
                }
            }
            _historyRepository.SaveUpdated(familyIdentity, history);

            return new InitializeHistoryResult(true);
        }
    }
}
