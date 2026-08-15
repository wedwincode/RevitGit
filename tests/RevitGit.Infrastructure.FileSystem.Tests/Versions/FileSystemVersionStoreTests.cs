using System;
using System.IO;
using System.Linq;
using RevitGit.Domain.Identifiers;
using RevitGit.Domain.Snapshots;
using RevitGit.Infrastructure.FileSystem.Exceptions;
using RevitGit.Infrastructure.FileSystem.Repositories;
using RevitGit.Infrastructure.FileSystem.Serialization;
using RevitGit.Infrastructure.FileSystem.Tests.Helpers;
using RevitGit.Infrastructure.FileSystem.Tests.Serialization;
using RevitGit.Infrastructure.FileSystem.Versions;
using Xunit;

namespace RevitGit.Infrastructure.FileSystem.Tests.Versions
{
    public sealed class FileSystemVersionStoreTests
    {
        private static readonly DateTimeOffset CreatedAt =
            new DateTimeOffset(2026, 8, 15, 10, 0, 0, TimeSpan.Zero);

        [Fact]
        public void SaveVersionCopiesFullBinaryAndSnapshot()
        {
            using (var fixture = new Fixture(new byte[] { 1, 2, 3 }))
            {
                var id = VersionId();
                var snapshot = SnapshotJsonSerializerTests.Snapshot();

                fixture.Store.SaveVersion(fixture.FamilyPath, id, snapshot);
                var directory = fixture.VersionDirectory(id);

                Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(Path.Combine(directory, "family.rfa")));
                Assert.True(File.Exists(Path.Combine(directory, "snapshot.json")));
                Assert.True(File.Exists(Path.Combine(directory, "version.json")));
                Assert.Equal(snapshot, fixture.Store.LoadSnapshot(fixture.FamilyPath, id));
            }
        }

        [Fact]
        public void SourceChangesAfterSaveDoNotChangeStoredVersion()
        {
            using (var fixture = new Fixture(new byte[] { 1, 2, 3 }))
            {
                var id = VersionId();
                fixture.Store.SaveVersion(fixture.FamilyPath, id, SnapshotJsonSerializerTests.Snapshot());

                File.WriteAllBytes(fixture.FamilyPath, new byte[] { 9, 9, 9 });

                Assert.Equal(
                    new byte[] { 1, 2, 3 },
                    File.ReadAllBytes(Path.Combine(fixture.VersionDirectory(id), "family.rfa")));
            }
        }

        [Fact]
        public void ExistingVersionIsRejectedAndLeftUntouched()
        {
            using (var fixture = new Fixture(new byte[] { 1, 2, 3 }))
            {
                var id = VersionId();
                fixture.Store.SaveVersion(fixture.FamilyPath, id, SnapshotJsonSerializerTests.Snapshot());
                File.WriteAllBytes(fixture.FamilyPath, new byte[] { 9 });

                Assert.Throws<StorageConflictException>(
                    () => fixture.Store.SaveVersion(fixture.FamilyPath, id, SnapshotJsonSerializerTests.Snapshot()));
                Assert.Equal(
                    new byte[] { 1, 2, 3 },
                    File.ReadAllBytes(Path.Combine(fixture.VersionDirectory(id), "family.rfa")));
            }
        }

        [Fact]
        public void SnapshotFailureDoesNotPublishFinalVersionOrLeaveTempDirectory()
        {
            using (var fixture = new Fixture(new byte[] { 1 }, new ThrowingSnapshotSerializer()))
            {
                var id = VersionId();

                Assert.Throws<StorageException>(
                    () => fixture.Store.SaveVersion(fixture.FamilyPath, id, SnapshotJsonSerializerTests.Snapshot()));

                Assert.False(Directory.Exists(fixture.VersionDirectory(id)));
                Assert.Empty(Directory.GetDirectories(fixture.Repository.Paths.VersionsDirectory, ".tmp-*"));
            }
        }

        [Fact]
        public void OpeningRepositoryCleansOnlyStaleTempDirectories()
        {
            using (var fixture = new Fixture(new byte[] { 1 }))
            {
                var stale = Path.Combine(fixture.Repository.Paths.VersionsDirectory, ".tmp-stale");
                var regular = Path.Combine(fixture.Repository.Paths.VersionsDirectory, "keep");
                Directory.CreateDirectory(stale);
                Directory.CreateDirectory(regular);

                fixture.Manager.Open(fixture.FamilyPath);

                Assert.False(Directory.Exists(stale));
                Assert.True(Directory.Exists(regular));
            }
        }

        [Fact]
        public void RestoreReplacesWorkingFamilyThroughSavedContent()
        {
            using (var fixture = new Fixture(new byte[] { 1, 2, 3 }))
            {
                var id = VersionId();
                fixture.Store.SaveVersion(fixture.FamilyPath, id, SnapshotJsonSerializerTests.Snapshot());
                File.WriteAllBytes(fixture.FamilyPath, new byte[] { 9, 9 });

                fixture.Store.RestoreVersion(fixture.FamilyPath, id);

                Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(fixture.FamilyPath));
                Assert.Empty(Directory.GetFiles(fixture.Temp.Path, ".tmp-*"));
            }
        }

        [Fact]
        public void UnknownVersionRestoreReturnsExplicitStorageError()
        {
            using (var fixture = new Fixture(new byte[] { 1 }))
            {
                Assert.Throws<RepositoryCorruptedException>(
                    () => fixture.Store.RestoreVersion(fixture.FamilyPath, VersionId()));
            }
        }

        [Fact]
        public void IntegrityVerificationSucceedsForUntouchedVersion()
        {
            using (var fixture = new Fixture(new byte[] { 1, 2, 3 }))
            {
                var id = VersionId();
                fixture.Store.SaveVersion(fixture.FamilyPath, id, SnapshotJsonSerializerTests.Snapshot());

                fixture.Store.VerifyVersion(fixture.FamilyPath, id);
            }
        }

        [Fact]
        public void BinaryCorruptionIsDetected()
        {
            using (var fixture = new Fixture(new byte[] { 1, 2, 3 }))
            {
                var id = VersionId();
                fixture.Store.SaveVersion(fixture.FamilyPath, id, SnapshotJsonSerializerTests.Snapshot());
                File.WriteAllBytes(Path.Combine(fixture.VersionDirectory(id), "family.rfa"), new byte[] { 7 });

                Assert.Throws<RepositoryCorruptedException>(
                    () => fixture.Store.VerifyVersion(fixture.FamilyPath, id));
            }
        }

        [Fact]
        public void SnapshotCorruptionIsDetected()
        {
            using (var fixture = new Fixture(new byte[] { 1, 2, 3 }))
            {
                var id = VersionId();
                fixture.Store.SaveVersion(fixture.FamilyPath, id, SnapshotJsonSerializerTests.Snapshot());
                File.AppendAllText(Path.Combine(fixture.VersionDirectory(id), "snapshot.json"), "corrupt");

                Assert.Throws<RepositoryCorruptedException>(
                    () => fixture.Store.VerifyVersion(fixture.FamilyPath, id));
            }
        }

        [Fact]
        public void MalformedRepositoryMetadataIsReportedAsCorruption()
        {
            using (var fixture = new Fixture(new byte[] { 1 }))
            {
                File.WriteAllText(fixture.Repository.Paths.MetadataPath, "{malformed");

                Assert.Throws<RepositoryCorruptedException>(() => fixture.Manager.Open(fixture.FamilyPath));
            }
        }

        private static VersionId VersionId()
        {
            return new VersionId(Guid.NewGuid());
        }

        private sealed class Fixture : IDisposable
        {
            public Fixture(byte[] familyContent, IFamilySnapshotSerializer serializer = null)
            {
                Temp = new TemporaryDirectory();
                FamilyPath = Temp.CreateFamily(content: familyContent);
                Manager = new FamilyRepositoryManager(new FixedClock(CreatedAt));
                Repository = Manager.Initialize(FamilyPath);
                Store = new FileSystemVersionStore(Manager, serializer ?? new SnapshotJsonSerializer());
            }

            public TemporaryDirectory Temp { get; }
            public string FamilyPath { get; }
            public FamilyRepositoryManager Manager { get; }
            public FamilyRepository Repository { get; }
            public FileSystemVersionStore Store { get; }

            public string VersionDirectory(VersionId id)
            {
                return Path.Combine(Repository.Paths.VersionsDirectory, id.Value.ToString("D"));
            }

            public void Dispose()
            {
                Temp.Dispose();
            }
        }

        private sealed class ThrowingSnapshotSerializer : IFamilySnapshotSerializer
        {
            public byte[] Serialize(FamilySnapshot snapshot)
            {
                throw new StorageException("Injected snapshot failure.");
            }

            public FamilySnapshot Deserialize(byte[] content)
            {
                throw new NotSupportedException();
            }
        }
    }
}
