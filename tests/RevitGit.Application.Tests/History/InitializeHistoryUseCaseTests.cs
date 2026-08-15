using System;
using RevitGit.Application.History;
using RevitGit.Application.Models;
using RevitGit.Application.Tests.Fakes;
using RevitGit.Domain.History;
using Xunit;

namespace RevitGit.Application.Tests.History
{
    public sealed class InitializeHistoryUseCaseTests
    {
        private static readonly DateTimeOffset Now =
            new DateTimeOffset(2026, 8, 15, 10, 0, 0, TimeSpan.Zero);

        [Fact]
        public void MissingHistoryCreatesMainVariantAndPersistsIt()
        {
            var identity = new FamilyIdentity("family-1");
            var repository = new FakeHistoryRepository();
            var useCase = new InitializeHistoryUseCase(
                repository,
                new FakeFamilyDocumentGateway(identity),
                new FakeClock(Now));

            var result = useCase.Execute("Main");

            Assert.True(result.WasCreated);
            Assert.Equal(1, repository.SaveCount);
            var history = repository.Load(identity);
            Assert.NotNull(history);
            Assert.Single(history.Variants);
            Assert.Equal("Main", history.GetVariant(history.CurrentVariantId).Name);
        }

        [Fact]
        public void ExistingHistoryIsNotOverwritten()
        {
            var identity = new FamilyIdentity("family-1");
            var existing = FamilyHistory.Create("Existing", Now.AddHours(-1), null);
            var repository = new FakeHistoryRepository();
            repository.Seed(identity, existing);
            var useCase = new InitializeHistoryUseCase(
                repository,
                new FakeFamilyDocumentGateway(identity),
                new FakeClock(Now));

            var result = useCase.Execute("Main");

            Assert.False(result.WasCreated);
            Assert.Equal(0, repository.SaveCount);
            Assert.Same(existing, repository.Load(identity));
        }
    }
}
