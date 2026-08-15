using System;
using System.IO;
using System.Linq;
using LibGit2Sharp;
using RevitGit.Application.Abstractions;
using RevitGit.Application.Models;
using RevitGit.Domain.History;
using RevitGit.Domain.Identifiers;
using RevitGit.Infrastructure.FileSystem.History;
using RevitGit.Infrastructure.FileSystem.Repositories;
using RevitGit.Infrastructure.FileSystem.Serialization;
using RevitGit.Domain.Snapshots;
using Xunit;

namespace RevitGit.Infrastructure.Git.Tests.Repository
{
    public sealed class LibGit2VersionRepositoryTests
    {
        [Fact]
        public void HistoricalSnapshotReaderReturnsNeutralSnapshotFromRequestedVersion()
        {
            using (var fixture = new GitFixture())
            {
                var serializer = new SnapshotJsonSerializer();
                var expected = new FamilySnapshot("Door", "Doors", new FamilyParameterSnapshot[0], new FamilyTypeSnapshot[0], "geometry-a");
                fixture.Snapshot = serializer.Serialize(expected);
                var history = fixture.CreateInitial("A", new byte[] { 1 });
                var versionId = history.Versions.Values.Single().Id;

                var reader = fixture.Reopen(serializer.Deserialize);

                Assert.Equal(expected, reader.ReadSnapshot(fixture.Identity, versionId));
            }
        }

        [Fact]
        public void SaveThreeVersions_CreatesLinearSingleParentGraphAndControlledTree()
        {
            using (var fixture = new GitFixture())
            {
                var history = fixture.CreateInitial("A", new byte[] { 0, 1, 2, 255 });
                var a = history.Versions.Values.Single().Id;
                var b = fixture.AddVersion(history, "B", new byte[] { 3, 0, 4 });
                var c = fixture.AddVersion(history, null, new byte[] { 5, 6 });

                using (var repository = new LibGit2Sharp.Repository(fixture.WorkDirectory))
                {
                    Assert.Equal(3, repository.Commits.Count());
                    Assert.Empty(repository.Lookup<Commit>(fixture.Adapter.GetStorageObjectId(a)).Parents);
                    Assert.Equal(a, fixture.Adapter.GetParentVersionId(b));
                    Assert.Equal(b, fixture.Adapter.GetParentVersionId(c));
                    Assert.Equal(new[] { "family.rfa", "snapshot.json", "version.json" },
                        repository.Head.Tip.Tree.Select(entry => entry.Name).OrderBy(name => name).ToArray());
                    Assert.False(repository.Info.IsHeadDetached);
                    Assert.Empty(repository.Network.Remotes);
                }
            }
        }

        [Fact]
        public void VariantFromOldVersion_ThenContinueBothVariants_CreatesDivergentGraphWithoutMerge()
        {
            using (var fixture = new GitFixture())
            {
                var history = fixture.CreateInitial("A", new byte[] { 1 });
                var a = history.Versions.Values.Single().Id;
                var b = fixture.AddVersion(history, "B", new byte[] { 2 });
                var c = fixture.AddVersion(history, "C", new byte[] { 3 });
                var main = history.CurrentVariantId;

                var secondary = history.CreateVariant(b, "Вариант / тест: 1");
                fixture.Adapter.Save(fixture.Identity, history);
                Assert.Equal(3, fixture.Adapter.CommitCount);
                Assert.Equal(b, secondary.CurrentVersionId);

                history.SwitchVariant(secondary.Id);
                fixture.Adapter.Save(fixture.Identity, history);
                var d = fixture.AddVersion(history, "Русский комментарий", new byte[] { 4, 0, 4 });
                Assert.Equal(b, fixture.Adapter.GetParentVersionId(d));

                history.SwitchVariant(main);
                fixture.Adapter.Save(fixture.Identity, history);
                var e = fixture.AddVersion(history, "E", new byte[] { 5 });

                Assert.Equal(c, fixture.Adapter.GetParentVersionId(e));
                Assert.Equal(c, history.GetVersion(e).ParentVersionId);
                Assert.Equal("variant/" + main.Value.ToString("N"), fixture.Adapter.CurrentInternalBranchName);
                Assert.DoesNotContain("Вариант", fixture.Adapter.GetInternalBranchName(secondary.Id));
                using (var repository = new LibGit2Sharp.Repository(fixture.WorkDirectory))
                {
                    Assert.All(repository.Commits, commit => Assert.True(commit.Parents.Count() <= 1));
                    Assert.False(repository.Info.IsHeadDetached);
                }
            }
        }

        [Fact]
        public void RestoreOldVersion_CopiesBinaryAndSnapshotButCommitsNewMetadataOnCurrentTip()
        {
            using (var fixture = new GitFixture())
            {
                var originalBinary = new byte[] { 0, 9, 0, 8, 255 };
                var originalSnapshot = fixture.Snapshot = new byte[] { 10, 0, 11 };
                var history = fixture.CreateInitial("A", originalBinary);
                var a = history.Versions.Values.Single().Id;
                fixture.AddVersion(history, "B", new byte[] { 2 });
                var c = fixture.AddVersion(history, "C", new byte[] { 3 });

                fixture.Adapter.RestoreVersionContent(fixture.Identity, a);
                fixture.Snapshot = fixture.Adapter.ReadVersionFile(a, "snapshot.json");
                var restored = VersionId.New();
                fixture.Adapter.StoreCurrentVersion(fixture.Identity, restored);
                history.AddRestoredVersion(restored, a, fixture.Clock.UtcNow.AddHours(5), "restore");
                fixture.Adapter.Save(fixture.Identity, history);

                Assert.Equal(originalBinary, File.ReadAllBytes(fixture.FamilyPath));
                Assert.Equal(originalBinary, fixture.Adapter.ReadVersionFile(restored, "family.rfa"));
                Assert.Equal(originalSnapshot, fixture.Adapter.ReadVersionFile(restored, "snapshot.json"));
                Assert.Equal(c, fixture.Adapter.GetParentVersionId(restored));
                Assert.NotEqual(fixture.Adapter.ReadVersionFile(a, "version.json"), fixture.Adapter.ReadVersionFile(restored, "version.json"));
            }
        }

        [Fact]
        public void PublishPreparedRestore_DoesNotReadStagedFileAfterRevitOpensIt()
        {
            using (var fixture = new GitFixture())
            {
                var sourceBinary = new byte[] { 7, 0, 7 };
                var history = fixture.CreateInitial("A", sourceBinary);
                var source = history.Versions.Values.Single().Id;
                var current = fixture.AddVersion(history, "B", new byte[] { 8, 0, 8 });
                var prepared = fixture.Adapter.PrepareRestoreContent(fixture.Identity, source, current);

                try
                {
                    using (File.Open(prepared.PreparedFamilyFilePath, FileMode.Open, FileAccess.Read, FileShare.None))
                    {
                        fixture.Adapter.PublishPreparedRestore(fixture.Identity, prepared);
                    }

                    Assert.Equal(sourceBinary, File.ReadAllBytes(fixture.FamilyPath));
                }
                finally
                {
                    fixture.Adapter.CleanupPreparedRestore(prepared);
                }
            }
        }

        [Fact]
        public void ReopenAndHistoricalRead_PreserveMappingsBranchAndCurrentWorkspace()
        {
            using (var fixture = new GitFixture())
            {
                var snapshotA = fixture.Snapshot = new byte[] { 1, 2, 3 };
                var history = fixture.CreateInitial("A", new byte[] { 7 });
                var a = history.Versions.Values.Single().Id;
                fixture.Snapshot = new byte[] { 4, 5, 6 };
                var b = fixture.AddVersion(history, "B", new byte[] { 8 });
                var branchBefore = fixture.Adapter.CurrentInternalBranchName;

                var reopened = fixture.Reopen();
                var loaded = reopened.Load(fixture.Identity);
                Assert.Equal(history.CurrentVariantId, loaded.CurrentVariantId);
                Assert.Equal(snapshotA, reopened.ReadVersionFile(a, "snapshot.json"));
                Assert.Equal(new byte[] { 7 }, reopened.ReadVersionFile(a, "family.rfa"));
                Assert.Equal(branchBefore, reopened.CurrentInternalBranchName);
                Assert.Equal(b, loaded.GetVariant(loaded.CurrentVariantId).CurrentVersionId);
            }
        }

        [Fact]
        public void DuplicateVersionSave_FailsWithoutMovingTip()
        {
            using (var fixture = new GitFixture())
            {
                var history = fixture.CreateInitial("A", new byte[] { 1 });
                var a = history.Versions.Values.Single().Id;
                var tip = fixture.Adapter.GetStorageObjectId(a);

                fixture.Adapter.Save(fixture.Identity, history);
                Assert.Equal(1, fixture.Adapter.CommitCount);

                Assert.Throws<GitStorageException>(() => fixture.Adapter.StoreCurrentVersion(fixture.Identity, a));
                Assert.Equal(tip, fixture.Adapter.GetStorageObjectId(a));
                Assert.Equal(1, fixture.Adapter.CommitCount);
            }
        }

        [Fact]
        public void StoreCurrentVersion_ReadsFamilyWhileRevitStyleWriteHandleRemainsOpen()
        {
            using (var fixture = new GitFixture())
            {
                var expected = new byte[] { 4, 3, 2, 1 };
                File.WriteAllBytes(fixture.FamilyPath, expected);
                var history = FamilyHistory.Create("Основной", fixture.Clock.UtcNow, "A");
                var versionId = history.Versions.Values.Single().Id;

                using (File.Open(
                    fixture.FamilyPath,
                    FileMode.Open,
                    FileAccess.ReadWrite,
                    FileShare.ReadWrite | FileShare.Delete))
                {
                    fixture.Adapter.StoreCurrentVersion(fixture.Identity, versionId);
                }

                fixture.Adapter.Save(fixture.Identity, history);
                Assert.Equal(expected, fixture.Adapter.ReadVersionFile(versionId, "family.rfa"));
            }
        }

        [Fact]
        public void StoreCurrentVersion_ReadFailureDoesNotLeavePendingDirectory()
        {
            using (var fixture = new GitFixture())
            using (File.Open(fixture.FamilyPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                Assert.Throws<GitStorageException>(
                    () => fixture.Adapter.StoreCurrentVersion(fixture.Identity, VersionId.New()));

                Assert.Empty(Directory.GetDirectories(fixture.RepositoryDirectory, ".pending-*"));
            }
        }

        [Fact]
        public void ReopenRemovesAbandonedPendingDirectory()
        {
            using (var fixture = new GitFixture())
            {
                var abandoned = Path.Combine(fixture.RepositoryDirectory, ".pending-abandoned");
                Directory.CreateDirectory(abandoned);
                File.WriteAllBytes(Path.Combine(abandoned, "partial"), new byte[] { 1 });

                fixture.Reopen();

                Assert.False(Directory.Exists(abandoned));
            }
        }

        [Fact]
        public void MissingMappedBranch_IsReportedAsCorruption()
        {
            using (var fixture = new GitFixture())
            {
                var history = fixture.CreateInitial("A", new byte[] { 1 });
                var branchName = fixture.Adapter.GetInternalBranchName(history.CurrentVariantId);
                using (var repository = new LibGit2Sharp.Repository(fixture.WorkDirectory))
                {
                    var orphan = repository.Branches.Add("orphan", repository.Head.Tip);
                    Commands.Checkout(repository, orphan);
                    repository.Branches.Remove(branchName);
                }

                Assert.Throws<GitRepositoryCorruptedException>(() => fixture.Reopen().Load(fixture.Identity));
            }
        }

        [Fact]
        public void InspectIntegrity_MissingMappedBranch_ReturnsStructuredIssue()
        {
            using (var fixture = new GitFixture())
            {
                var history = fixture.CreateInitial("A", new byte[] { 1 });
                var branchName = fixture.Adapter.GetInternalBranchName(history.CurrentVariantId);
                using (var repository = new LibGit2Sharp.Repository(fixture.WorkDirectory))
                {
                    var orphan = repository.Branches.Add("orphan", repository.Head.Tip);
                    Commands.Checkout(repository, orphan);
                    repository.Branches.Remove(branchName);
                }

                var result = fixture.Adapter.InspectIntegrity(history);

                Assert.False(result.IsValid);
                Assert.Contains(result.Issues, issue => issue.Code == "VARIANT_BRANCH_MISSING"
                    && issue.VariantId.Equals(history.CurrentVariantId));
            }
        }

        [Fact]
        public void WrongVersionMetadata_IsReportedAsCorruption()
        {
            using (var fixture = new GitFixture())
            {
                var history = fixture.CreateInitial("A", new byte[] { 1 });
                var a = history.Versions.Values.Single().Id;
                var metadataPath = Path.Combine(fixture.WorkDirectory, "version.json");
                File.WriteAllText(metadataPath,
                    File.ReadAllText(metadataPath).Replace(a.Value.ToString("D"), Guid.NewGuid().ToString("D")));
                using (var repository = new LibGit2Sharp.Repository(fixture.WorkDirectory))
                {
                    Commands.Stage(repository, "version.json");
                    var signature = new Signature("RevitGit", "local@revitgit.invalid", fixture.Clock.UtcNow);
                    repository.Commit("corrupt fixture", signature, signature);
                }

                Assert.Throws<GitRepositoryCorruptedException>(() => fixture.Reopen().Load(fixture.Identity));
            }
        }

        [Fact]
        public void FiftyVersions_ReopenEnumerateAndResolveAll()
        {
            using (var fixture = new GitFixture())
            {
                var history = fixture.CreateInitial("0", new byte[] { 0 });
                for (var index = 1; index < 50; index++)
                    fixture.AddVersion(history, index.ToString(), new[] { (byte)index, (byte)0 });

                var reopened = fixture.Reopen();
                var loaded = reopened.Load(fixture.Identity);
                Assert.Equal(50, loaded.Versions.Count);
                foreach (var versionId in loaded.Versions.Keys)
                    Assert.NotEmpty(reopened.ReadVersionFile(versionId, "snapshot.json"));
            }
        }

        [Fact]
        public void MoveFamilyWithAdjacentHistory_ReopensWithoutAbsoluteRepositoryIdentity()
        {
            var sourceRoot = Path.Combine(Path.GetTempPath(), "RevitGit-GitMoveSource-" + Guid.NewGuid().ToString("N"));
            var targetRoot = Path.Combine(Path.GetTempPath(), "RevitGit-GitMoveTarget-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(sourceRoot);
            try
            {
                var sourceFamily = Path.Combine(sourceRoot, "Door.rfa");
                File.WriteAllBytes(sourceFamily, new byte[] { 1, 0, 2 });
                var clock = new FixedClock();
                var manager = new FamilyRepositoryManager(clock);
                var storage = manager.Initialize(sourceFamily);
                var metadata = new FileSystemHistoryRepository(manager);
                var identity = new FamilyIdentity(sourceFamily);
                var adapter = new LibGit2VersionRepository(storage.Paths.RepositoryDirectory, metadata, () => new byte[] { 9 });
                var history = FamilyHistory.Create("Основной", clock.UtcNow, "A");
                adapter.StoreCurrentVersion(identity, history.Versions.Values.Single().Id);
                adapter.Save(identity, history);

                Directory.Move(sourceRoot, targetRoot);
                var movedFamily = Path.Combine(targetRoot, "Door.rfa");
                var movedManager = new FamilyRepositoryManager(clock);
                var movedStorage = movedManager.Open(movedFamily);
                var movedIdentity = new FamilyIdentity(movedFamily);
                var reopened = new LibGit2VersionRepository(movedStorage.Paths.RepositoryDirectory,
                    new FileSystemHistoryRepository(movedManager), () => new byte[] { 9 });

                Assert.Single(reopened.Load(movedIdentity).Versions);
                Assert.Equal(new byte[] { 1, 0, 2 }, reopened.ReadVersionFile(
                    history.Versions.Values.Single().Id, "family.rfa"));
            }
            finally
            {
                var cleanup = Directory.Exists(targetRoot) ? targetRoot : sourceRoot;
                if (Directory.Exists(cleanup))
                {
                    foreach (var path in Directory.GetFileSystemEntries(cleanup, "*", SearchOption.AllDirectories))
                        File.SetAttributes(path, FileAttributes.Normal);
                    Directory.Delete(cleanup, true);
                }
            }
        }

        private sealed class GitFixture : IDisposable
        {
            private readonly string _root;
            private readonly FileSystemHistoryRepository _metadata;

            public GitFixture()
            {
                _root = Path.Combine(Path.GetTempPath(), "RevitGit-GitTests-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(_root);
                FamilyPath = Path.Combine(_root, "Door.rfa");
                File.WriteAllBytes(FamilyPath, new byte[] { 0 });
                Identity = new FamilyIdentity(FamilyPath);
                Clock = new FixedClock();
                var manager = new FamilyRepositoryManager(Clock);
                var familyRepository = manager.Initialize(FamilyPath);
                RepositoryDirectory = familyRepository.Paths.RepositoryDirectory;
                WorkDirectory = Path.Combine(RepositoryDirectory, "repo");
                _metadata = new FileSystemHistoryRepository(manager);
                Adapter = NewAdapter();
                Snapshot = new byte[] { 1 };
            }

            public string FamilyPath { get; }
            public string RepositoryDirectory { get; }
            public string WorkDirectory { get; }
            public FamilyIdentity Identity { get; }
            public FixedClock Clock { get; }
            public byte[] Snapshot { get; set; }
            public LibGit2VersionRepository Adapter { get; }

            public FamilyHistory CreateInitial(string comment, byte[] binary)
            {
                File.WriteAllBytes(FamilyPath, binary);
                var history = FamilyHistory.Create("Основной", Clock.UtcNow, comment);
                Adapter.StoreCurrentVersion(Identity, history.Versions.Values.Single().Id);
                Adapter.Save(Identity, history);
                return history;
            }

            public VersionId AddVersion(FamilyHistory history, string comment, byte[] binary)
            {
                File.WriteAllBytes(FamilyPath, binary);
                Snapshot = new[] { binary[0], (byte)42 };
                var id = VersionId.New();
                Adapter.StoreCurrentVersion(Identity, id);
                history.AddVersion(id, Clock.UtcNow.AddHours(Adapter.CommitCount), comment);
                Adapter.Save(Identity, history);
                return id;
            }

            public LibGit2VersionRepository Reopen()
            {
                return NewAdapter();
            }

            public LibGit2VersionRepository Reopen(Func<byte[], FamilySnapshot> deserializer)
            {
                return new LibGit2VersionRepository(RepositoryDirectory, _metadata, () => Snapshot, deserializer);
            }

            public void Dispose()
            {
                if (!Directory.Exists(_root)) return;
                foreach (var path in Directory.GetFileSystemEntries(_root, "*", SearchOption.AllDirectories))
                {
                    File.SetAttributes(path, FileAttributes.Normal);
                }
                Directory.Delete(_root, true);
            }

            private LibGit2VersionRepository NewAdapter()
            {
                return new LibGit2VersionRepository(RepositoryDirectory, _metadata, () => Snapshot);
            }
        }

        private sealed class FixedClock : IClock
        {
            public DateTimeOffset UtcNow { get; } = new DateTimeOffset(2026, 8, 15, 10, 0, 0, TimeSpan.Zero);
        }
    }
}
