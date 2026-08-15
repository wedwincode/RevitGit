using RevitGit.Application.Abstractions;
using RevitGit.Application.Models;
using RevitGit.Domain.Identifiers;

namespace RevitGit.Application.History
{
    public sealed class CreateVariantUseCase
    {
        private readonly IHistoryRepository _historyRepository;
        private readonly IFamilyDocumentGateway _documentGateway;

        public CreateVariantUseCase(
            IHistoryRepository historyRepository,
            IFamilyDocumentGateway documentGateway)
        {
            _historyRepository = historyRepository;
            _documentGateway = documentGateway;
        }

        public VariantSummary Execute(VersionId fromVersionId, string name)
        {
            var familyIdentity = _documentGateway.GetIdentity();
            var history = _historyRepository.LoadRequired(familyIdentity);
            var variant = history.CreateVariant(fromVersionId, name);
            _historyRepository.SaveUpdated(familyIdentity, history);

            return new VariantSummary(
                variant.Id,
                variant.Name,
                variant.CurrentVersionId,
                false);
        }

    }
}
