using System;
using RevitGit.Application.Exceptions;
using RevitGit.Application.History;
using RevitGit.Application.Models;
using RevitGit.Application.Tests.Fakes;
using RevitGit.Domain.History;
using Xunit;

namespace RevitGit.Application.Tests.History
{
    public sealed class RestoreVersionUseCaseTests
    {
        private static readonly DateTimeOffset InitialTime =
            new DateTimeOffset(2026, 8, 15, 10, 0, 0, TimeSpan.Zero);

        [Fact]
        public void RestoreOldVersionRestoresContentAndAddsNewVersionAtCurrentTip()
        {
            var fixture = CreateFixture();
            var first = fixture.History.GetVariant(fixture.History.CurrentVariantId).CurrentVersionId;
            fixture.History.AddVersion(InitialTime.AddMinutes(1), null);
            var third = fixture.History.AddVersion(InitialTime.AddMinutes(2), null);

            var restored = fixture.UseCase.Execute(first, null);

            Assert.Equal(first, fixture.ContentStore.RestoredVersionId);
            Assert.Equal(third.Id, restored.ParentVersionId);
            Assert.Equal(first, restored.RestoredFromVersionId);
            Assert.Equal("Restored from version " + first, restored.Comment);
            Assert.Equal(restored.Id, fixture.History.GetVariant(fixture.History.CurrentVariantId).CurrentVersionId);
            Assert.Equal(1, fixture.Repository.SaveCount);
        }

        [Fact]
        public void ExplicitRestoreCommentIsPreservedAndNormalizedByDomain()
        {
            var fixture = CreateFixture();
            var source = fixture.History.GetVariant(fixture.History.CurrentVariantId).CurrentVersionId;

            var restored = fixture.UseCase.Execute(source, "  Return to approved state  ");

            Assert.Equal("Return to approved state", restored.Comment);
        }

        [Fact]
        public void PhysicalRestoreFailureDoesNotChangeOrPersistHistory()
        {
            var fixture = CreateFixture();
            var source = fixture.History.GetVariant(fixture.History.CurrentVariantId).CurrentVersionId;
            var versionCount = fixture.History.Versions.Count;
            fixture.ContentStore.RestoreException = new InvalidOperationException("restore failed");

            var error = Assert.Throws<ApplicationOperationException>(
                () => fixture.UseCase.Execute(source, null));

            Assert.Equal(ApplicationFailureStage.RestoreVersionContent, error.Stage);
            Assert.Equal(versionCount, fixture.History.Versions.Count);
            Assert.Equal(source, fixture.History.GetVariant(fixture.History.CurrentVariantId).CurrentVersionId);
            Assert.Equal(0, fixture.Repository.SaveCount);
        }

        [Fact]
        public void RestoreInsideSecondaryVariantOnlyAdvancesThatVariant()
        {
            var fixture = CreateFixture();
            var mainId = fixture.History.CurrentVariantId;
            var first = fixture.History.GetVariant(mainId).CurrentVersionId;
            var second = fixture.History.AddVersion(InitialTime.AddMinutes(1), null);
            var third = fixture.History.AddVersion(InitialTime.AddMinutes(2), null);
            var variant = fixture.History.CreateVariant(second.Id, "Alternative");
            fixture.History.SwitchVariant(variant.Id);
            var fourth = fixture.History.AddVersion(InitialTime.AddMinutes(3), null);

            var restored = fixture.UseCase.Execute(first, null);

            Assert.Equal(fourth.Id, restored.ParentVersionId);
            Assert.Equal(first, restored.RestoredFromVersionId);
            Assert.Equal(third.Id, fixture.History.GetVariant(mainId).CurrentVersionId);
            Assert.Equal(restored.Id, fixture.History.GetVariant(variant.Id).CurrentVersionId);
        }

        private static Fixture CreateFixture()
        {
            var identity = new FamilyIdentity("family-1");
            var history = FamilyHistory.Create("Main", InitialTime, null);
            var repository = new FakeHistoryRepository();
            repository.Seed(identity, history);
            var contentStore = new FakeVersionContentStore();
            var useCase = new RestoreVersionUseCase(
                repository,
                new FakeFamilyDocumentGateway(identity),
                contentStore,
                new FakeClock(InitialTime.AddMinutes(10)));
            return new Fixture(history, repository, contentStore, useCase);
        }

        private sealed class Fixture
        {
            public Fixture(
                FamilyHistory history,
                FakeHistoryRepository repository,
                FakeVersionContentStore contentStore,
                RestoreVersionUseCase useCase)
            {
                History = history;
                Repository = repository;
                ContentStore = contentStore;
                UseCase = useCase;
            }

            public FamilyHistory History { get; }
            public FakeHistoryRepository Repository { get; }
            public FakeVersionContentStore ContentStore { get; }
            public RestoreVersionUseCase UseCase { get; }
        }
    }
}
