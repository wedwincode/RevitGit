using System;
using System.IO;
using RevitGit.Infrastructure.FileSystem.Repositories;
using RevitGit.Infrastructure.FileSystem.Tests.Helpers;
using Xunit;

namespace RevitGit.Infrastructure.FileSystem.Tests.Repositories
{
    public sealed class FamilyRepositoryInitializationTests
    {
        private static readonly DateTimeOffset CreatedAt =
            new DateTimeOffset(2026, 8, 15, 10, 0, 0, TimeSpan.Zero);

        [Fact]
        public void LocatorProducesNormalizedDeterministicPaths()
        {
            using (var temp = new TemporaryDirectory())
            {
                var repositoryId = Guid.Parse("11111111-1111-1111-1111-111111111111");
                var familyPath = Path.Combine(temp.Path, "child", "..", "Door.rfa");

                var paths = new FamilyRepositoryLocator().Locate(familyPath, repositoryId);

                Assert.Equal(Path.Combine(temp.Path, "Door.rfa"), paths.FamilyFilePath);
                Assert.Equal(Path.Combine(temp.Path, ".familyhistory"), paths.HistoryRoot);
                Assert.Equal(Path.Combine(temp.Path, ".familyhistory", "index.json"), paths.IndexPath);
                Assert.Equal(
                    Path.Combine(temp.Path, ".familyhistory", "repositories", repositoryId.ToString("N")),
                    paths.RepositoryDirectory);
                Assert.Equal(Path.Combine(paths.RepositoryDirectory, "repository.json"), paths.MetadataPath);
                Assert.Equal(Path.Combine(paths.RepositoryDirectory, "history.json"), paths.HistoryPath);
                Assert.Equal(Path.Combine(paths.RepositoryDirectory, "versions"), paths.VersionsDirectory);
            }
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("Door.rfa")]
        [InlineData("C:\\Test\\Door.txt")]
        [InlineData("C:\\Test\\Project.rvt")]
        public void LocatorRejectsInvalidFamilyPaths(string path)
        {
            Assert.Throws<ArgumentException>(
                () => new FamilyRepositoryLocator().Locate(path, Guid.NewGuid()));
        }

        [Fact]
        public void InitializeCreatesHiddenRootVersionedMetadataAndVersionsDirectory()
        {
            using (var temp = new TemporaryDirectory())
            {
                var familyPath = temp.CreateFamily();
                var manager = Manager();

                var repository = manager.Initialize(familyPath);

                Assert.True(Directory.Exists(repository.Paths.HistoryRoot));
                Assert.True((File.GetAttributes(repository.Paths.HistoryRoot) & FileAttributes.Hidden) != 0);
                Assert.True(File.Exists(repository.Paths.IndexPath));
                Assert.True(File.Exists(repository.Paths.MetadataPath));
                Assert.True(Directory.Exists(repository.Paths.VersionsDirectory));
                Assert.Equal(1, repository.Metadata.StorageFormatVersion);
                Assert.Equal("Door.rfa", repository.Metadata.FamilyFileName);
                Assert.Equal(CreatedAt, repository.Metadata.CreatedAt);
                Assert.NotEqual(Guid.Empty, repository.Metadata.RepositoryId);
            }
        }

        [Fact]
        public void InitializeTwiceIsIdempotentAndKeepsExistingContent()
        {
            using (var temp = new TemporaryDirectory())
            {
                var familyPath = temp.CreateFamily();
                var manager = Manager();
                var first = manager.Initialize(familyPath);
                var marker = Path.Combine(first.Paths.VersionsDirectory, "keep.txt");
                File.WriteAllText(marker, "keep");

                var second = manager.Initialize(familyPath);

                Assert.Equal(first.Metadata.RepositoryId, second.Metadata.RepositoryId);
                Assert.True(File.Exists(marker));
            }
        }

        [Fact]
        public void RepositoryIdSurvivesMovingFamilyAndHistoryTogether()
        {
            using (var temp = new TemporaryDirectory())
            {
                var originalDirectory = Path.Combine(temp.Path, "Original");
                Directory.CreateDirectory(originalDirectory);
                var familyPath = Path.Combine(originalDirectory, "Door.rfa");
                File.WriteAllBytes(familyPath, new byte[] { 1 });
                var manager = Manager();
                var original = manager.Initialize(familyPath);
                var movedDirectory = Path.Combine(temp.Path, "Moved");

                Directory.Move(originalDirectory, movedDirectory);
                var moved = manager.Open(Path.Combine(movedDirectory, "Door.rfa"));

                Assert.Equal(original.Metadata.RepositoryId, moved.Metadata.RepositoryId);
            }
        }

        [Fact]
        public void SingleRepositoryCanBeReassociatedAfterExternalRename()
        {
            using (var temp = new TemporaryDirectory())
            {
                var originalPath = temp.CreateFamily();
                var manager = Manager();
                var original = manager.Initialize(originalPath);
                var renamedPath = Path.Combine(temp.Path, "Door_Main.rfa");
                File.Move(originalPath, renamedPath);

                var renamed = manager.Open(renamedPath);

                Assert.Equal(original.Metadata.RepositoryId, renamed.Metadata.RepositoryId);
                Assert.Equal("Door_Main.rfa", renamed.Metadata.FamilyFileName);
                Assert.DoesNotContain(temp.Path, File.ReadAllText(renamed.Paths.MetadataPath));
                Assert.DoesNotContain(temp.Path, File.ReadAllText(renamed.Paths.IndexPath));
            }
        }

        [Fact]
        public void MissingRepositoryIsNormalForExistsCheck()
        {
            using (var temp = new TemporaryDirectory())
            {
                var familyPath = temp.CreateFamily();

                Assert.False(Manager().Exists(familyPath));
            }
        }

        [Fact]
        public void FamiliesInSameDirectoryUseIndependentRepositories()
        {
            using (var temp = new TemporaryDirectory())
            {
                var doorPath = temp.CreateFamily("Door.rfa");
                var windowPath = temp.CreateFamily("Window.rfa");
                var manager = Manager();

                var door = manager.Initialize(doorPath);
                var window = manager.Initialize(windowPath);

                Assert.NotEqual(door.Metadata.RepositoryId, window.Metadata.RepositoryId);
                Assert.NotEqual(door.Paths.RepositoryDirectory, window.Paths.RepositoryDirectory);
                Assert.Equal(door.Metadata.RepositoryId, manager.Open(doorPath).Metadata.RepositoryId);
                Assert.Equal(window.Metadata.RepositoryId, manager.Open(windowPath).Metadata.RepositoryId);
            }
        }

        [Fact]
        public void DirectoryWithRfaExtensionIsRejected()
        {
            using (var temp = new TemporaryDirectory())
            {
                var directoryPath = Path.Combine(temp.Path, "NotAFile.rfa");
                Directory.CreateDirectory(directoryPath);

                Assert.Throws<ArgumentException>(
                    () => new FamilyRepositoryLocator().Locate(directoryPath, Guid.NewGuid()));
            }
        }

        private static FamilyRepositoryManager Manager()
        {
            return new FamilyRepositoryManager(new FixedClock(CreatedAt));
        }
    }
}
