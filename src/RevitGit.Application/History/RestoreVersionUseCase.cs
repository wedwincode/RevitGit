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
            var familyIdentity = _documentGateway.GetIdentity();
            var history = _historyRepository.LoadRequired(familyIdentity);
            history.GetVersion(sourceVersionId);

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

            var effectiveComment = string.IsNullOrWhiteSpace(comment)
                ? "Restored from version " + sourceVersionId
                : comment;
            var version = history.AddRestoredVersion(sourceVersionId, _clock.UtcNow, effectiveComment);
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
