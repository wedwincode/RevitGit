using RevitGit.Application.Abstractions;
using RevitGit.Application.Models;
using RevitGit.Domain.History;

namespace RevitGit.Application.History
{
    public sealed class InitializeHistoryUseCase
    {
        private readonly IHistoryRepository _historyRepository;
        private readonly IFamilyDocumentGateway _documentGateway;
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

        public InitializeHistoryResult Execute(string mainVariantName)
        {
            var familyIdentity = _documentGateway.GetIdentity();
            if (_historyRepository.Exists(familyIdentity))
            {
                return new InitializeHistoryResult(false);
            }

            var history = FamilyHistory.Create(mainVariantName, _clock.UtcNow, null);
            _historyRepository.SaveUpdated(familyIdentity, history);

            return new InitializeHistoryResult(true);
        }
    }
}
