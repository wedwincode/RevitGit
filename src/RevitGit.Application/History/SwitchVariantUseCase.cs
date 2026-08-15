using RevitGit.Application.Abstractions;
using RevitGit.Application.Models;
using RevitGit.Domain.Identifiers;

namespace RevitGit.Application.History
{
    public sealed class SwitchVariantUseCase
    {
        private readonly IHistoryRepository _historyRepository;
        private readonly IFamilyDocumentGateway _documentGateway;

        public SwitchVariantUseCase(
            IHistoryRepository historyRepository,
            IFamilyDocumentGateway documentGateway)
        {
            _historyRepository = historyRepository;
            _documentGateway = documentGateway;
        }

        public VariantSummary Execute(VariantId variantId)
        {
            var familyIdentity = _documentGateway.GetIdentity();
            var history = _historyRepository.LoadRequired(familyIdentity);
            history.SwitchVariant(variantId);
            var variant = history.GetVariant(variantId);
            _historyRepository.SaveUpdated(familyIdentity, history);

            return new VariantSummary(
                variant.Id,
                variant.Name,
                variant.CurrentVersionId,
                true);
        }

    }
}
