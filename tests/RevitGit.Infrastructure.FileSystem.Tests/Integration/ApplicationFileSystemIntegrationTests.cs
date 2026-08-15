using System;
using System.IO;
using RevitGit.Application.Abstractions;
using RevitGit.Application.History;
using RevitGit.Application.Models;
using RevitGit.Domain.Snapshots;
using RevitGit.Infrastructure.FileSystem.History;
using RevitGit.Infrastructure.FileSystem.Repositories;
using RevitGit.Infrastructure.FileSystem.Serialization;
using RevitGit.Infrastructure.FileSystem.Tests.Helpers;
using RevitGit.Infrastructure.FileSystem.Tests.Serialization;
using RevitGit.Infrastructure.FileSystem.Versions;
using Xunit;

namespace RevitGit.Infrastructure.FileSystem.Tests.Integration
{
    public sealed class ApplicationFileSystemIntegrationTests
    {
        private static readonly DateTimeOffset InitialTime =
            new DateTimeOffset(2026, 8, 15, 10, 0, 0, TimeSpan.Zero);

        [Fact]
        public void InitializeHistoryUseCaseCreatesAndReopensRealFilesystemHistory()
        {
            using (var temp = new TemporaryDirectory())
            {
                var familyPath = temp.CreateFamily();
                var fixture = Fixture(familyPath);
                var useCase = new InitializeHistoryUseCase(
                    fixture.HistoryRepository,
                    fixture.Document,
                    new FixedClock(InitialTime));

                var first = useCase.Execute("Main");
                var second = useCase.Execute("Main");

                Assert.True(first.WasCreated);
                Assert.False(second.WasCreated);
                Assert.NotNull(fixture.HistoryRepository.Load(fixture.Identity));
            }
        }

        [Fact]
        public void SaveVersionUseCaseStoresRealBinarySnapshotAndHistory()
        {
            using (var temp = new TemporaryDirectory())
            {
                var familyPath = temp.CreateFamily(content: new byte[] { 1, 2, 3 });
                var fixture = Fixture(familyPath);
                new InitializeHistoryUseCase(
                    fixture.HistoryRepository,
                    fixture.Document,
                    new FixedClock(InitialTime)).Execute("Main");
                var contentStore = new FileSystemVersionContentStore(
                    fixture.Manager,
                    new FixedSnapshotProvider(SnapshotJsonSerializerTests.Snapshot()),
                    new SnapshotJsonSerializer());
                var useCase = new SaveVersionUseCase(
                    fixture.HistoryRepository,
                    fixture.Document,
                    contentStore,
                    new FixedClock(InitialTime.AddMinutes(1)));

                var saved = useCase.Execute("Approved");

                Assert.True(fixture.Document.SaveCalled);
                Assert.Equal(
                    new byte[] { 1, 2, 3 },
                    File.ReadAllBytes(Path.Combine(
                        fixture.Manager.Open(familyPath).Paths.VersionsDirectory,
                        saved.Id.Value.ToString("D"),
                        "family.rfa")));
                Assert.Equal(
                    SnapshotJsonSerializerTests.Snapshot(),
                    contentStore.LoadSnapshot(fixture.Identity, saved.Id));
                Assert.Equal(
                    "Approved",
                    fixture.HistoryRepository.Load(fixture.Identity).GetVersion(saved.Id).Comment);
            }
        }

        [Fact]
        public void RestoreVersionUseCasePhysicallyRestoresAndPersistsHistory()
        {
            using (var temp = new TemporaryDirectory())
            {
                var familyPath = temp.CreateFamily(content: new byte[] { 1, 2, 3 });
                var fixture = Fixture(familyPath);
                new InitializeHistoryUseCase(
                    fixture.HistoryRepository,
                    fixture.Document,
                    new FixedClock(InitialTime)).Execute("Main");
                var history = fixture.HistoryRepository.Load(fixture.Identity);
                var sourceId = history.GetVariant(history.CurrentVariantId).CurrentVersionId;
                var versionStore = new FileSystemVersionStore(fixture.Manager, new SnapshotJsonSerializer());
                versionStore.SaveVersion(familyPath, sourceId, SnapshotJsonSerializerTests.Snapshot());
                File.WriteAllBytes(familyPath, new byte[] { 9, 9 });
                var contentStore = new FileSystemVersionContentStore(
                    fixture.Manager,
                    new FixedSnapshotProvider(SnapshotJsonSerializerTests.Snapshot()),
                    new SnapshotJsonSerializer());
                var useCase = new RestoreVersionUseCase(
                    fixture.HistoryRepository,
                    fixture.Document,
                    contentStore,
                    new FixedClock(InitialTime.AddMinutes(2)));

                var restored = useCase.Execute(sourceId, "Restore approved");

                Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(familyPath));
                var loaded = fixture.HistoryRepository.Load(fixture.Identity);
                Assert.Equal(sourceId, loaded.GetVersion(restored.Id).RestoredFromVersionId);
                Assert.Equal("Restore approved", loaded.GetVersion(restored.Id).Comment);
                Assert.Equal(
                    SnapshotJsonSerializerTests.Snapshot(),
                    contentStore.LoadSnapshot(fixture.Identity, restored.Id));
            }
        }

        private static FixtureState Fixture(string familyPath)
        {
            var identity = new FamilyIdentity(familyPath);
            var manager = new FamilyRepositoryManager(new FixedClock(InitialTime));
            return new FixtureState(
                identity,
                manager,
                new FileSystemHistoryRepository(manager),
                new FakeDocumentGateway(identity));
        }

        private sealed class FixtureState
        {
            public FixtureState(
                FamilyIdentity identity,
                FamilyRepositoryManager manager,
                FileSystemHistoryRepository historyRepository,
                FakeDocumentGateway document)
            {
                Identity = identity;
                Manager = manager;
                HistoryRepository = historyRepository;
                Document = document;
            }

            public FamilyIdentity Identity { get; }
            public FamilyRepositoryManager Manager { get; }
            public FileSystemHistoryRepository HistoryRepository { get; }
            public FakeDocumentGateway Document { get; }
        }

        private sealed class FakeDocumentGateway : IFamilyDocumentGateway
        {
            private readonly FamilyIdentity _identity;

            public FakeDocumentGateway(FamilyIdentity identity)
            {
                _identity = identity;
            }

            public bool SaveCalled { get; private set; }

            public FamilyIdentity GetIdentity()
            {
                return _identity;
            }

            public void Save()
            {
                SaveCalled = true;
            }
        }

        private sealed class FixedSnapshotProvider : IFamilySnapshotProvider
        {
            private readonly FamilySnapshot _snapshot;

            public FixedSnapshotProvider(FamilySnapshot snapshot)
            {
                _snapshot = snapshot;
            }

            public FamilySnapshot CaptureSnapshot()
            {
                return _snapshot;
            }
        }
    }
}
