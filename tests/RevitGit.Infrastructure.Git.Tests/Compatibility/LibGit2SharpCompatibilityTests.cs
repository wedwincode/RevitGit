using System;
using System.IO;
using LibGit2Sharp;
using Xunit;

namespace RevitGit.Infrastructure.Git.Tests.Compatibility
{
    public sealed class LibGit2SharpCompatibilityTests
    {
        [Fact]
        public void Net48_CanInitializeCommitAndReadRepository()
        {
            var directory = Path.Combine(Path.GetTempPath(), "RevitGit-GitSpike-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);

            try
            {
                LibGit2Sharp.Repository.Init(directory);
                using (var repository = new LibGit2Sharp.Repository(directory))
                {
                    File.WriteAllText(Path.Combine(directory, "controlled.txt"), "compatibility");
                    Commands.Stage(repository, "controlled.txt");
                    var signature = new Signature(
                        "RevitGit",
                        "local@revitgit.invalid",
                        new DateTimeOffset(2026, 8, 15, 0, 0, 0, TimeSpan.Zero));

                    var created = repository.Commit("Compatibility smoke", signature, signature);
                    var readBack = repository.Lookup<Commit>(created.Id);

                    Assert.NotNull(readBack);
                    Assert.Equal(created.Id, readBack.Id);
                    Assert.Equal("compatibility", ((Blob)readBack.Tree["controlled.txt"].Target).GetContentText());
                }
            }
            finally
            {
                if (Directory.Exists(directory))
                {
                    foreach (var path in Directory.GetFileSystemEntries(directory, "*", SearchOption.AllDirectories))
                    {
                        File.SetAttributes(path, FileAttributes.Normal);
                    }

                    Directory.Delete(directory, true);
                }
            }
        }
    }
}
