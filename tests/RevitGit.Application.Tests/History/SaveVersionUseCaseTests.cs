using System;
using System.Collections.Generic;
using RevitGit.Application.Exceptions;
using RevitGit.Application.History;
using RevitGit.Application.Models;
using RevitGit.Application.Tests.Fakes;
using RevitGit.Domain.History;
using Xunit;

namespace RevitGit.Application.Tests.History
{
    public sealed class SaveVersionUseCaseTests
    {
        private static readonly DateTimeOffset InitialTime =
            new DateTimeOffset(2026, 8, 15, 10, 0, 0, TimeSpan.Zero);

        [Fact]
        public void SaveVersionSavesDocumentStoresContentAddsVersionAndPersistsHistory()
        {
            var operations = new List<string>();
            var fixture = CreateFixture(operations);

            var created = fixture.UseCase.Execute("Changed width");

            Assert.True(fixture.Document.SaveCalled);
            Assert.True(fixture.ContentStore.StoreCalled);
            Assert.Equal(created.Id, fixture.ContentStore.StoredVersionId);
            Assert.Equal("Changed width", created.Comment);
            Assert.Equal(created.Id, fixture.History.GetVariant(fixture.History.CurrentVariantId).CurrentVersionId);
            Assert.Equal(1, fixture.Repository.SaveCount);
            Assert.Equal(new[] { "document-save", "content-store", "history-save" }, operations);
        }

        [Fact]
        public void SaveVersionInitializesMissingHistoryAsSingleCommentedVersion()
        {
            var operations = new List<string>();
            var identity = new FamilyIdentity("family-1");
            var repository = new FakeHistoryRepository(operations);
            var document = new FakeFamilyDocumentGateway(identity, operations);
            var contentStore = new FakeVersionContentStore(operations);
            var useCase = new SaveVersionUseCase(
                repository,
                document,
                contentStore,
                new FakeClock(InitialTime));

            var created = useCase.Execute("Начальная версия", "Основной");

            var history = repository.Load(identity);
            Assert.Single(history.Versions);
            Assert.Single(history.Variants);
            Assert.Equal("Основной", history.GetVariant(history.CurrentVariantId).Name);
            Assert.Equal(created.Id, history.GetVariant(history.CurrentVariantId).CurrentVersionId);
            Assert.Equal("Начальная версия", created.Comment);
            Assert.Null(created.ParentVersionId);
            Assert.Equal(new[] { "document-save", "content-store", "history-save" }, operations);
        }

        [Fact]
        public void SubsequentVersionUsesPreviousCurrentVersionAsParent()
        {
            var fixture = CreateFixture();
            var previous = fixture.History.GetVariant(fixture.History.CurrentVariantId).CurrentVersionId;

            var created = fixture.UseCase.Execute(null);

            Assert.Equal(previous, created.ParentVersionId);
            Assert.Equal(created.Id, fixture.History.GetVariant(fixture.History.CurrentVariantId).CurrentVersionId);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void MissingCommentIsNormalizedByDomain(string comment)
        {
            var fixture = CreateFixture();

            var created = fixture.UseCase.Execute(comment);

            Assert.Null(created.Comment);
        }

        [Fact]
        public void DocumentSaveFailureDoesNotStoreContentOrChangeHistory()
        {
            var fixture = CreateFixture();
            var versionCount = fixture.History.Versions.Count;
            var current = fixture.History.GetVariant(fixture.History.CurrentVariantId).CurrentVersionId;
            fixture.Document.SaveException = new InvalidOperationException("save failed");

            var error = Assert.Throws<ApplicationOperationException>(() => fixture.UseCase.Execute(null));

            Assert.Equal(ApplicationFailureStage.SaveDocument, error.Stage);
            Assert.False(fixture.ContentStore.StoreCalled);
            Assert.Equal(versionCount, fixture.History.Versions.Count);
            Assert.Equal(current, fixture.History.GetVariant(fixture.History.CurrentVariantId).CurrentVersionId);
            Assert.Equal(0, fixture.Repository.SaveCount);
        }

        [Fact]
        public void ContentStoreFailureDoesNotChangeHistory()
        {
            var fixture = CreateFixture();
            var versionCount = fixture.History.Versions.Count;
            var current = fixture.History.GetVariant(fixture.History.CurrentVariantId).CurrentVersionId;
            fixture.ContentStore.StoreException = new InvalidOperationException("store failed");

            var error = Assert.Throws<ApplicationOperationException>(() => fixture.UseCase.Execute(null));

            Assert.Equal(ApplicationFailureStage.StoreVersionContent, error.Stage);
            Assert.True(fixture.Document.SaveCalled);
            Assert.Equal(versionCount, fixture.History.Versions.Count);
            Assert.Equal(current, fixture.History.GetVariant(fixture.History.CurrentVariantId).CurrentVersionId);
            Assert.Equal(0, fixture.Repository.SaveCount);
        }

        private static Fixture CreateFixture(IList<string> operations = null)
        {
            var identity = new FamilyIdentity("family-1");
            var history = FamilyHistory.Create("Main", InitialTime, null);
            var repository = new FakeHistoryRepository(operations);
            repository.Seed(identity, history);
            var document = new FakeFamilyDocumentGateway(identity, operations);
            var contentStore = new FakeVersionContentStore(operations);
            var useCase = new SaveVersionUseCase(
                repository,
                document,
                contentStore,
                new FakeClock(InitialTime.AddMinutes(1)));
            return new Fixture(useCase, repository, document, contentStore, history);
        }

        private sealed class Fixture
        {
            public Fixture(
                SaveVersionUseCase useCase,
                FakeHistoryRepository repository,
                FakeFamilyDocumentGateway document,
                FakeVersionContentStore contentStore,
                FamilyHistory history)
            {
                UseCase = useCase;
                Repository = repository;
                Document = document;
                ContentStore = contentStore;
                History = history;
            }

            public SaveVersionUseCase UseCase { get; }
            public FakeHistoryRepository Repository { get; }
            public FakeFamilyDocumentGateway Document { get; }
            public FakeVersionContentStore ContentStore { get; }
            public FamilyHistory History { get; }
        }
    }
}
