using System;
using System.IO;
using System.Linq;
using RevitGit.Application.Abstractions;
using RevitGit.Application.History;
using RevitGit.Application.Models;
using RevitGit.Domain.Identifiers;
using RevitGit.Infrastructure.FileSystem.History;
using RevitGit.Infrastructure.FileSystem.Repositories;
using Xunit;

namespace RevitGit.Infrastructure.Git.Tests.Integration
{
    public sealed class ApplicationGitIntegrationTests
    {
        [Fact]
        public void SaveVersionOnRepositoryWithoutHistoryCreatesOneInitialVersion()
        {
            var root = Path.Combine(Path.GetTempPath(), "RevitGit-GitFirstSave-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                var familyPath = Path.Combine(root, "Door.rfa");
                File.WriteAllBytes(familyPath, new byte[] { 1, 2, 3 });
                var clock = new FixedClock();
                var document = new DocumentGateway(familyPath);
                var manager = new FamilyRepositoryManager(clock);
                var familyRepository = manager.Initialize(familyPath);
                var metadata = new FileSystemHistoryRepository(manager);
                var adapter = new LibGit2VersionRepository(
                    familyRepository.Paths.RepositoryDirectory,
                    metadata,
                    () => new byte[] { 9, 8, 7 });

                var created = new SaveVersionUseCase(adapter, document, adapter, clock)
                    .Execute("Начальная версия", "Основной");

                var history = adapter.Load(document.GetIdentity());
                Assert.Single(history.Versions);
                Assert.Single(history.Variants);
                Assert.Equal("Основной", history.GetVariant(history.CurrentVariantId).Name);
                Assert.Equal(created.Id, history.GetVariant(history.CurrentVariantId).CurrentVersionId);
                Assert.Equal("Начальная версия", created.Comment);
                Assert.Equal(1, adapter.CommitCount);
                Assert.Equal(new byte[] { 9, 8, 7 }, adapter.ReadVersionFile(created.Id, "snapshot.json"));
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    foreach (var path in Directory.GetFileSystemEntries(root, "*", SearchOption.AllDirectories))
                        File.SetAttributes(path, FileAttributes.Normal);
                    Directory.Delete(root, true);
                }
            }
        }

        [Fact]
        public void CoreUseCases_RunAgainstRealGitBackedContracts()
        {
            var root = Path.Combine(Path.GetTempPath(), "RevitGit-GitApplication-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                var familyPath = Path.Combine(root, "Door.rfa");
                File.WriteAllBytes(familyPath, new byte[] { 1 });
                var clock = new FixedClock();
                var document = new DocumentGateway(familyPath);
                var manager = new FamilyRepositoryManager(clock);
                var familyRepository = manager.Initialize(familyPath);
                var metadata = new FileSystemHistoryRepository(manager);
                byte[] snapshot = { 1, 0, 1 };
                var adapter = new LibGit2VersionRepository(
                    familyRepository.Paths.RepositoryDirectory, metadata, () => snapshot);

                var initialized = new InitializeHistoryUseCase(adapter, document, adapter, clock).Execute("Основной");
                Assert.True(initialized.WasCreated);
                var initial = metadata.Load(document.GetIdentity()).Versions.Values.Single().Id;

                File.WriteAllBytes(familyPath, new byte[] { 2 });
                snapshot = new byte[] { 2, 0, 2 };
                var second = new SaveVersionUseCase(adapter, document, adapter, clock).Execute("Вторая");
                var variant = new CreateVariantUseCase(adapter, document).Execute(initial, "Ответвление / тест");
                new SwitchVariantUseCase(adapter, document).Execute(variant.Id);

                File.WriteAllBytes(familyPath, new byte[] { 3 });
                snapshot = new byte[] { 3, 0, 3 };
                var branchVersion = new SaveVersionUseCase(adapter, document, adapter, clock).Execute("Третья");
                snapshot = adapter.ReadVersionFile(initial, "snapshot.json");
                var restored = new RestoreVersionUseCase(adapter, document, adapter, clock).Execute(initial);
                var summary = new GetHistoryUseCase(adapter, document).Execute();

                Assert.Equal(initial, second.ParentVersionId);
                Assert.Equal(initial, branchVersion.ParentVersionId);
                Assert.Equal(branchVersion.Id, restored.ParentVersionId);
                Assert.Equal(initial, restored.RestoredFromVersionId);
                Assert.Equal(new[] { restored.Id, branchVersion.Id, initial }, summary.Versions.Select(item => item.Id));
                Assert.Equal(2, summary.Variants.Count);
                Assert.Equal(4, adapter.CommitCount);
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    foreach (var path in Directory.GetFileSystemEntries(root, "*", SearchOption.AllDirectories))
                        File.SetAttributes(path, FileAttributes.Normal);
                    Directory.Delete(root, true);
                }
            }
        }

        private sealed class FixedClock : IClock
        {
            public DateTimeOffset UtcNow { get; } = new DateTimeOffset(2026, 8, 15, 12, 0, 0, TimeSpan.Zero);
        }

        private sealed class DocumentGateway : IFamilyDocumentGateway
        {
            private readonly FamilyIdentity _identity;
            public DocumentGateway(string path) { _identity = new FamilyIdentity(path); }
            public FamilyIdentity GetIdentity() { return _identity; }
            public void Save() { }
        }
    }
}
