using System;
using System.Globalization;
using System.IO;
using System.Runtime.Serialization;
using RevitGit.Domain.Identifiers;
using RevitGit.Domain.Snapshots;
using RevitGit.Infrastructure.FileSystem.Exceptions;
using RevitGit.Infrastructure.FileSystem.Integrity;
using RevitGit.Infrastructure.FileSystem.Repositories;
using RevitGit.Infrastructure.FileSystem.Serialization;

namespace RevitGit.Infrastructure.FileSystem.Versions
{
    public sealed class FileSystemVersionStore
    {
        private readonly FamilyRepositoryManager _repositoryManager;
        private readonly IFamilySnapshotSerializer _snapshotSerializer;
        private readonly StorageJsonSerializer _jsonSerializer;
        private readonly ChecksumService _checksumService;

        public FileSystemVersionStore(
            FamilyRepositoryManager repositoryManager,
            IFamilySnapshotSerializer snapshotSerializer)
        {
            _repositoryManager = repositoryManager ?? throw new ArgumentNullException(nameof(repositoryManager));
            _snapshotSerializer = snapshotSerializer ?? throw new ArgumentNullException(nameof(snapshotSerializer));
            _jsonSerializer = new StorageJsonSerializer();
            _checksumService = new ChecksumService();
        }

        public void SaveVersion(string familyFilePath, VersionId versionId, FamilySnapshot snapshot)
        {
            if (versionId == null)
            {
                throw new ArgumentNullException(nameof(versionId));
            }

            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            var repository = _repositoryManager.Open(familyFilePath);
            var finalDirectory = GetVersionDirectory(repository, versionId);
            if (Directory.Exists(finalDirectory))
            {
                throw new StorageConflictException("Version content already exists for " + versionId + ".");
            }

            var temporaryDirectory = Path.Combine(
                repository.Paths.VersionsDirectory,
                ".tmp-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temporaryDirectory);

            try
            {
                var familyPath = Path.Combine(temporaryDirectory, "family.rfa");
                var snapshotPath = Path.Combine(temporaryDirectory, "snapshot.json");
                var metadataPath = Path.Combine(temporaryDirectory, "version.json");
                File.Copy(repository.Paths.FamilyFilePath, familyPath, false);
                WriteFile(snapshotPath, _snapshotSerializer.Serialize(snapshot));

                var metadata = new VersionMetadataDto
                {
                    StorageFormatVersion = StorageFormat.CurrentVersion,
                    VersionId = versionId.Value.ToString("D"),
                    FamilySha256 = _checksumService.ComputeSha256(familyPath),
                    SnapshotSha256 = _checksumService.ComputeSha256(snapshotPath)
                };
                WriteFile(metadataPath, _jsonSerializer.Serialize(metadata));

                if (!File.Exists(familyPath) || !File.Exists(snapshotPath) || !File.Exists(metadataPath))
                {
                    throw new StorageException("Version content could not be validated before publishing.");
                }

                Directory.Move(temporaryDirectory, finalDirectory);
            }
            catch (StorageException)
            {
                throw;
            }
            catch (Exception exception) when (
                exception is IOException
                || exception is UnauthorizedAccessException)
            {
                throw new StorageException("Version content could not be stored.", exception);
            }
            finally
            {
                if (Directory.Exists(temporaryDirectory))
                {
                    Directory.Delete(temporaryDirectory, true);
                }
            }
        }

        public FamilySnapshot LoadSnapshot(string familyFilePath, VersionId versionId)
        {
            var paths = VerifyAndGetPaths(familyFilePath, versionId);
            return _snapshotSerializer.Deserialize(File.ReadAllBytes(paths.SnapshotPath));
        }

        public void RestoreVersion(string familyFilePath, VersionId versionId)
        {
            var paths = VerifyAndGetPaths(familyFilePath, versionId);
            var repository = _repositoryManager.Open(familyFilePath);
            var workingDirectory = Path.GetDirectoryName(repository.Paths.FamilyFilePath);
            var temporaryPath = Path.Combine(
                workingDirectory,
                ".tmp-" + Guid.NewGuid().ToString("N") + ".rfa");

            try
            {
                File.Copy(paths.FamilyPath, temporaryPath, false);
                if (File.Exists(repository.Paths.FamilyFilePath))
                {
                    File.Replace(temporaryPath, repository.Paths.FamilyFilePath, null);
                }
                else
                {
                    File.Move(temporaryPath, repository.Paths.FamilyFilePath);
                }
            }
            catch (Exception exception) when (
                exception is IOException
                || exception is UnauthorizedAccessException)
            {
                throw new StorageException("Version content could not be restored.", exception);
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
        }

        public void VerifyVersion(string familyFilePath, VersionId versionId)
        {
            VerifyAndGetPaths(familyFilePath, versionId);
        }

        private VersionPaths VerifyAndGetPaths(string familyFilePath, VersionId versionId)
        {
            if (versionId == null)
            {
                throw new ArgumentNullException(nameof(versionId));
            }

            var repository = _repositoryManager.Open(familyFilePath);
            var directory = GetVersionDirectory(repository, versionId);
            var paths = new VersionPaths(
                Path.Combine(directory, "family.rfa"),
                Path.Combine(directory, "snapshot.json"),
                Path.Combine(directory, "version.json"));
            if (!Directory.Exists(directory)
                || !File.Exists(paths.FamilyPath)
                || !File.Exists(paths.SnapshotPath)
                || !File.Exists(paths.MetadataPath))
            {
                throw new RepositoryCorruptedException(
                    "Version content is missing for " + versionId + ".");
            }

            VersionMetadataDto metadata;
            try
            {
                metadata = _jsonSerializer.Deserialize<VersionMetadataDto>(
                    File.ReadAllBytes(paths.MetadataPath),
                    "Version metadata");
            }
            catch (IOException exception)
            {
                throw new StorageException("Version metadata could not be read.", exception);
            }

            Guid storedVersionId;
            if (metadata == null
                || metadata.StorageFormatVersion != StorageFormat.CurrentVersion
                || !Guid.TryParse(metadata.VersionId, out storedVersionId)
                || storedVersionId != versionId.Value
                || string.IsNullOrWhiteSpace(metadata.FamilySha256)
                || string.IsNullOrWhiteSpace(metadata.SnapshotSha256))
            {
                throw new RepositoryCorruptedException("Version metadata is incomplete or inconsistent.");
            }

            if (!string.Equals(
                    metadata.FamilySha256,
                    _checksumService.ComputeSha256(paths.FamilyPath),
                    StringComparison.Ordinal)
                || !string.Equals(
                    metadata.SnapshotSha256,
                    _checksumService.ComputeSha256(paths.SnapshotPath),
                    StringComparison.Ordinal))
            {
                throw new RepositoryCorruptedException("Version content checksum verification failed.");
            }

            return paths;
        }

        private static string GetVersionDirectory(FamilyRepository repository, VersionId versionId)
        {
            return Path.Combine(repository.Paths.VersionsDirectory, versionId.Value.ToString("D"));
        }

        private static void WriteFile(string path, byte[] content)
        {
            using (var stream = new FileStream(
                path,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                4096,
                FileOptions.WriteThrough))
            {
                stream.Write(content, 0, content.Length);
                stream.Flush(true);
            }
        }

        [DataContract]
        private sealed class VersionMetadataDto
        {
            [DataMember(Name = "storageFormatVersion", Order = 1)] public int StorageFormatVersion { get; set; }
            [DataMember(Name = "versionId", Order = 2)] public string VersionId { get; set; }
            [DataMember(Name = "familySha256", Order = 3)] public string FamilySha256 { get; set; }
            [DataMember(Name = "snapshotSha256", Order = 4)] public string SnapshotSha256 { get; set; }
        }

        private sealed class VersionPaths
        {
            public VersionPaths(string familyPath, string snapshotPath, string metadataPath)
            {
                FamilyPath = familyPath;
                SnapshotPath = snapshotPath;
                MetadataPath = metadataPath;
            }

            public string FamilyPath { get; }
            public string SnapshotPath { get; }
            public string MetadataPath { get; }
        }
    }
}
