using System;
using RevitGit.Application.History;
using RevitGit.Application.Models;
using RevitGit.Application.Tests.Fakes;
using RevitGit.Domain.Exceptions;
using RevitGit.Domain.History;
using RevitGit.Domain.Identifiers;
using Xunit;

namespace RevitGit.Application.Tests.History
{
    public sealed class VariantUseCaseTests
    {
        private static readonly DateTimeOffset InitialTime =
            new DateTimeOffset(2026, 8, 15, 10, 0, 0, TimeSpan.Zero);

        [Fact]
        public void CreateVariantFromOldVersionPersistsWithoutSwitchingCurrentVariant()
        {
            var fixture = CreateFixture();
            var mainId = fixture.History.CurrentVariantId;
            var second = fixture.History.AddVersion(InitialTime.AddMinutes(1), null);
            var third = fixture.History.AddVersion(InitialTime.AddMinutes(2), null);

            var created = fixture.CreateVariant.Execute(second.Id, "Alternative");

            Assert.Equal(second.Id, created.CurrentVersionId);
            Assert.Equal("Alternative", created.Name);
            Assert.False(created.IsCurrent);
            Assert.Equal(mainId, fixture.History.CurrentVariantId);
            Assert.Equal(third.Id, fixture.History.GetVariant(mainId).CurrentVersionId);
            Assert.Equal(1, fixture.Repository.SaveCount);
        }

        [Fact]
        public void CreateVariantFromUnknownVersionDoesNotChangeOrPersistHistory()
        {
            var fixture = CreateFixture();
            var variantCount = fixture.History.Variants.Count;
            var unknown = new VersionId(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));

            Assert.Throws<UnknownVersionException>(
                () => fixture.CreateVariant.Execute(unknown, "Alternative"));

            Assert.Equal(variantCount, fixture.History.Variants.Count);
            Assert.Equal(0, fixture.Repository.SaveCount);
        }

        [Fact]
        public void SwitchVariantChangesCurrentVariantAndPersistsWithoutCreatingVersion()
        {
            var fixture = CreateFixture();
            var from = fixture.History.GetVariant(fixture.History.CurrentVariantId).CurrentVersionId;
            var variant = fixture.History.CreateVariant(from, "Alternative");
            var versionCount = fixture.History.Versions.Count;

            var switched = fixture.SwitchVariant.Execute(variant.Id);

            Assert.Equal(variant.Id, fixture.History.CurrentVariantId);
            Assert.True(switched.IsCurrent);
            Assert.Equal(versionCount, fixture.History.Versions.Count);
            Assert.Equal(1, fixture.Repository.SaveCount);
        }

        [Fact]
        public void SwitchUnknownVariantDoesNotChangeOrPersistHistory()
        {
            var fixture = CreateFixture();
            var current = fixture.History.CurrentVariantId;
            var unknown = new VariantId(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"));

            Assert.Throws<UnknownVariantException>(() => fixture.SwitchVariant.Execute(unknown));

            Assert.Equal(current, fixture.History.CurrentVariantId);
            Assert.Equal(0, fixture.Repository.SaveCount);
        }

        private static Fixture CreateFixture()
        {
            var identity = new FamilyIdentity("family-1");
            var history = FamilyHistory.Create("Main", InitialTime, null);
            var repository = new FakeHistoryRepository();
            repository.Seed(identity, history);
            var document = new FakeFamilyDocumentGateway(identity);
            return new Fixture(
                history,
                repository,
                new CreateVariantUseCase(repository, document),
                new SwitchVariantUseCase(repository, document));
        }

        private sealed class Fixture
        {
            public Fixture(
                FamilyHistory history,
                FakeHistoryRepository repository,
                CreateVariantUseCase createVariant,
                SwitchVariantUseCase switchVariant)
            {
                History = history;
                Repository = repository;
                CreateVariant = createVariant;
                SwitchVariant = switchVariant;
            }

            public FamilyHistory History { get; }
            public FakeHistoryRepository Repository { get; }
            public CreateVariantUseCase CreateVariant { get; }
            public SwitchVariantUseCase SwitchVariant { get; }
        }
    }
}
