using System;
using System.IO;
using System.Linq;
using RevitGit.Application.Models;
using RevitGit.Domain.History;
using RevitGit.Infrastructure.FileSystem.Exceptions;
using RevitGit.Infrastructure.FileSystem.History;
using RevitGit.Infrastructure.FileSystem.Repositories;
using RevitGit.Infrastructure.FileSystem.Tests.Helpers;
using Xunit;

namespace RevitGit.Infrastructure.FileSystem.Tests.History
{
    public sealed class FileSystemHistoryRepositoryTests
    {
        private static readonly DateTimeOffset InitialTime =
            new DateTimeOffset(2026, 8, 15, 10, 0, 0, TimeSpan.Zero);

        [Fact]
        public void HistoryRoundTripPreservesGraphAndDoesNotMutateOriginal()
        {
            using (var temp = new TemporaryDirectory())
            {
                var familyPath = temp.CreateFamily();
                var identity = new FamilyIdentity(familyPath);
                var repository = Repository();
                var history = FamilyHistory.Create("Main", InitialTime, "Initial");
                var mainId = history.CurrentVariantId;
                var first = history.GetVariant(mainId).CurrentVersionId;
                var second = history.AddVersion(InitialTime.AddMinutes(1), "Second");
                var third = history.AddVersion(InitialTime.AddMinutes(2), null);
                var alternative = history.CreateVariant(second.Id, "Alternative");
                history.SwitchVariant(alternative.Id);
                var restored = history.AddRestoredVersion(
                    first, InitialTime.AddMinutes(3), "Restored first");
                var originalVersionIds = history.Versions.Keys.ToArray();
                var originalVariantIds = history.Variants.Keys.ToArray();

                repository.Save(identity, history);
                var loaded = repository.Load(identity);

                Assert.NotSame(history, loaded);
                Assert.Equal(history.CurrentVariantId, loaded.CurrentVariantId);
                Assert.Equal(history.Versions.Count, loaded.Versions.Count);
                Assert.Equal(history.Variants.Count, loaded.Variants.Count);
                Assert.Equal(third.Id, loaded.GetVariant(mainId).CurrentVersionId);
                Assert.Equal(restored.Id, loaded.GetVariant(alternative.Id).CurrentVersionId);
                Assert.Equal(first, loaded.GetVersion(restored.Id).RestoredFromVersionId);
                Assert.Equal("Restored first", loaded.GetVersion(restored.Id).Comment);
                Assert.Equal(InitialTime.AddMinutes(3), loaded.GetVersion(restored.Id).CreatedAt);
                Assert.Equal(originalVersionIds, history.Versions.Keys.ToArray());
                Assert.Equal(originalVariantIds, history.Variants.Keys.ToArray());
            }
        }

        [Fact]
        public void MissingHistoryReturnsFalseAndNull()
        {
            using (var temp = new TemporaryDirectory())
            {
                var identity = new FamilyIdentity(temp.CreateFamily());
                var repository = Repository();

                Assert.False(repository.Exists(identity));
                Assert.Null(repository.Load(identity));
            }
        }

        [Fact]
        public void MalformedHistoryJsonIsReportedAsCorruption()
        {
            using (var temp = new TemporaryDirectory())
            {
                var familyPath = temp.CreateFamily();
                var identity = new FamilyIdentity(familyPath);
                var manager = Manager();
                var repository = new FileSystemHistoryRepository(manager);
                repository.Save(identity, FamilyHistory.Create("Main", InitialTime, null));
                var paths = manager.Open(familyPath).Paths;
                File.WriteAllText(paths.HistoryPath, "{malformed");

                Assert.Throws<RepositoryCorruptedException>(() => repository.Load(identity));
            }
        }

        [Fact]
        public void HistoryJsonDoesNotPersistAbsoluteFamilyPath()
        {
            using (var temp = new TemporaryDirectory())
            {
                var familyPath = temp.CreateFamily();
                var identity = new FamilyIdentity(familyPath);
                var manager = Manager();
                var repository = new FileSystemHistoryRepository(manager);
                repository.Save(identity, FamilyHistory.Create("Main", InitialTime, null));

                Assert.DoesNotContain(
                    temp.Path,
                    File.ReadAllText(manager.Open(familyPath).Paths.HistoryPath));
            }
        }

        private static FileSystemHistoryRepository Repository()
        {
            return new FileSystemHistoryRepository(Manager());
        }

        private static FamilyRepositoryManager Manager()
        {
            return new FamilyRepositoryManager(new FixedClock(InitialTime));
        }
    }
}
