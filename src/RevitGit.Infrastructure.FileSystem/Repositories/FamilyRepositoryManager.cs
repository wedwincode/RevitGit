using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using RevitGit.Application.Abstractions;
using RevitGit.Infrastructure.FileSystem.Exceptions;
using RevitGit.Infrastructure.FileSystem.IO;
using RevitGit.Infrastructure.FileSystem.Serialization;

namespace RevitGit.Infrastructure.FileSystem.Repositories
{
    public sealed class FamilyRepositoryManager
    {
        private readonly IClock _clock;
        private readonly FamilyRepositoryLocator _locator;
        private readonly StorageJsonSerializer _serializer;
        private readonly AtomicFileWriter _writer;

        public FamilyRepositoryManager(IClock clock)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _locator = new FamilyRepositoryLocator();
            _serializer = new StorageJsonSerializer();
            _writer = new AtomicFileWriter();
        }

        public bool Exists(string familyFilePath)
        {
            var normalizedPath = RequireExistingFamily(familyFilePath);
            var historyRoot = GetHistoryRoot(normalizedPath);
            var indexPath = Path.Combine(historyRoot, "index.json");
            if (!Directory.Exists(historyRoot) || !File.Exists(indexPath))
            {
                return false;
            }

            try
            {
                Open(normalizedPath);
                return true;
            }
            catch (RepositoryNotFoundException)
            {
                return false;
            }
        }

        public FamilyRepository Initialize(string familyFilePath)
        {
            var normalizedPath = RequireExistingFamily(familyFilePath);
            var historyRoot = GetHistoryRoot(normalizedPath);
            Directory.CreateDirectory(historyRoot);
            EnsureHidden(historyRoot);
            Directory.CreateDirectory(Path.Combine(historyRoot, "repositories"));

            var indexPath = Path.Combine(historyRoot, "index.json");
            var index = File.Exists(indexPath) ? ReadIndex(indexPath) : RepositoryIndexDto.Create();
            RepositoryIndexEntryDto entry;
            if (TryResolveEntry(index, normalizedPath, out entry))
            {
                return OpenResolved(normalizedPath, index, entry, indexPath);
            }

            var repositoryId = Guid.NewGuid();
            var paths = _locator.Locate(normalizedPath, repositoryId);
            Directory.CreateDirectory(paths.RepositoryDirectory);
            Directory.CreateDirectory(paths.VersionsDirectory);
            var metadata = new FamilyRepositoryMetadata(
                StorageFormat.CurrentVersion,
                repositoryId,
                Path.GetFileName(normalizedPath),
                _clock.UtcNow);
            WriteMetadata(paths.MetadataPath, metadata);

            index.Families.Add(new RepositoryIndexEntryDto
            {
                FamilyFileName = metadata.FamilyFileName,
                RepositoryId = repositoryId.ToString("D")
            });
            WriteIndex(indexPath, index);
            return new FamilyRepository(paths, metadata);
        }

        public FamilyRepository Open(string familyFilePath)
        {
            var normalizedPath = RequireExistingFamily(familyFilePath);
            var historyRoot = GetHistoryRoot(normalizedPath);
            var indexPath = Path.Combine(historyRoot, "index.json");
            if (!Directory.Exists(historyRoot) || !File.Exists(indexPath))
            {
                throw new RepositoryNotFoundException(Path.GetFileName(normalizedPath));
            }

            EnsureHidden(historyRoot);
            var index = ReadIndex(indexPath);
            RepositoryIndexEntryDto entry;
            if (!TryResolveEntry(index, normalizedPath, out entry))
            {
                throw new RepositoryNotFoundException(Path.GetFileName(normalizedPath));
            }

            return OpenResolved(normalizedPath, index, entry, indexPath);
        }

        private FamilyRepository OpenResolved(
            string familyFilePath,
            RepositoryIndexDto index,
            RepositoryIndexEntryDto entry,
            string indexPath)
        {
            Guid repositoryId;
            if (!Guid.TryParse(entry.RepositoryId, out repositoryId) || repositoryId == Guid.Empty)
            {
                throw new RepositoryCorruptedException("Repository index contains an invalid repository identifier.");
            }

            var paths = _locator.Locate(familyFilePath, repositoryId);
            if (!File.Exists(paths.MetadataPath))
            {
                throw new RepositoryCorruptedException("Repository metadata is missing.");
            }

            var metadata = ReadMetadata(paths.MetadataPath);
            if (metadata.RepositoryId != repositoryId)
            {
                throw new RepositoryCorruptedException("Repository metadata identifier does not match the index.");
            }

            var currentFileName = Path.GetFileName(familyFilePath);
            if (!string.Equals(entry.FamilyFileName, currentFileName, StringComparison.OrdinalIgnoreCase))
            {
                entry.FamilyFileName = currentFileName;
                metadata = new FamilyRepositoryMetadata(
                    metadata.StorageFormatVersion,
                    metadata.RepositoryId,
                    currentFileName,
                    metadata.CreatedAt);
                WriteMetadata(paths.MetadataPath, metadata);
                WriteIndex(indexPath, index);
            }

            if (!Directory.Exists(paths.VersionsDirectory))
            {
                throw new RepositoryCorruptedException("Repository versions directory is missing.");
            }

            CleanupTemporaryDirectories(paths.VersionsDirectory);
            return new FamilyRepository(paths, metadata);
        }

        private bool TryResolveEntry(
            RepositoryIndexDto index,
            string familyFilePath,
            out RepositoryIndexEntryDto entry)
        {
            var fileName = Path.GetFileName(familyFilePath);
            entry = index.Families.FirstOrDefault(
                item => string.Equals(item.FamilyFileName, fileName, StringComparison.OrdinalIgnoreCase));
            if (entry != null)
            {
                return true;
            }

            if (index.Families.Count == 1)
            {
                var only = index.Families[0];
                var oldFamilyPath = Path.Combine(Path.GetDirectoryName(familyFilePath), only.FamilyFileName);
                if (!File.Exists(oldFamilyPath))
                {
                    entry = only;
                    return true;
                }
            }

            return false;
        }

        private RepositoryIndexDto ReadIndex(string path)
        {
            RepositoryIndexDto index;
            try
            {
                index = _serializer.Deserialize<RepositoryIndexDto>(File.ReadAllBytes(path), "Repository index");
            }
            catch (IOException exception)
            {
                throw new StorageException("Repository index could not be read.", exception);
            }

            if (index == null
                || index.StorageFormatVersion != StorageFormat.CurrentVersion
                || index.Families == null)
            {
                throw new RepositoryCorruptedException("Repository index has an unsupported or incomplete format.");
            }

            return index;
        }

        private FamilyRepositoryMetadata ReadMetadata(string path)
        {
            RepositoryMetadataDto dto;
            try
            {
                dto = _serializer.Deserialize<RepositoryMetadataDto>(File.ReadAllBytes(path), "Repository metadata");
            }
            catch (IOException exception)
            {
                throw new StorageException("Repository metadata could not be read.", exception);
            }

            Guid repositoryId;
            DateTimeOffset createdAt;
            if (dto == null
                || dto.StorageFormatVersion != StorageFormat.CurrentVersion
                || !Guid.TryParse(dto.RepositoryId, out repositoryId)
                || repositoryId == Guid.Empty
                || string.IsNullOrWhiteSpace(dto.FamilyFileName)
                || !DateTimeOffset.TryParseExact(
                    dto.CreatedAt,
                    "O",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out createdAt))
            {
                throw new RepositoryCorruptedException("Repository metadata has an unsupported or incomplete format.");
            }

            return new FamilyRepositoryMetadata(
                dto.StorageFormatVersion,
                repositoryId,
                dto.FamilyFileName,
                createdAt);
        }

        private void WriteIndex(string path, RepositoryIndexDto index)
        {
            index.Families.Sort((left, right) =>
                StringComparer.OrdinalIgnoreCase.Compare(left.FamilyFileName, right.FamilyFileName));
            _writer.Write(path, _serializer.Serialize(index));
        }

        private void WriteMetadata(string path, FamilyRepositoryMetadata metadata)
        {
            var dto = new RepositoryMetadataDto
            {
                StorageFormatVersion = metadata.StorageFormatVersion,
                RepositoryId = metadata.RepositoryId.ToString("D"),
                FamilyFileName = metadata.FamilyFileName,
                CreatedAt = metadata.CreatedAt.ToString("O", CultureInfo.InvariantCulture)
            };
            _writer.Write(path, _serializer.Serialize(dto));
        }

        private string RequireExistingFamily(string familyFilePath)
        {
            var normalized = _locator.NormalizeFamilyPath(familyFilePath);
            if (!File.Exists(normalized))
            {
                throw new ArgumentException("Family file does not exist.", nameof(familyFilePath));
            }

            return normalized;
        }

        private static string GetHistoryRoot(string familyFilePath)
        {
            return Path.Combine(Path.GetDirectoryName(familyFilePath), ".familyhistory");
        }

        private static void EnsureHidden(string historyRoot)
        {
            var attributes = File.GetAttributes(historyRoot);
            if ((attributes & FileAttributes.Hidden) == 0)
            {
                File.SetAttributes(historyRoot, attributes | FileAttributes.Hidden);
            }
        }

        private static void CleanupTemporaryDirectories(string versionsDirectory)
        {
            foreach (var directory in Directory.GetDirectories(versionsDirectory, ".tmp-*", SearchOption.TopDirectoryOnly))
            {
                var name = Path.GetFileName(directory);
                if (name.StartsWith(".tmp-", StringComparison.Ordinal))
                {
                    Directory.Delete(directory, true);
                }
            }
        }

        [DataContract]
        private sealed class RepositoryIndexDto
        {
            [DataMember(Name = "storageFormatVersion", Order = 1)]
            public int StorageFormatVersion { get; set; }

            [DataMember(Name = "families", Order = 2)]
            public List<RepositoryIndexEntryDto> Families { get; set; }

            public static RepositoryIndexDto Create()
            {
                return new RepositoryIndexDto
                {
                    StorageFormatVersion = StorageFormat.CurrentVersion,
                    Families = new List<RepositoryIndexEntryDto>()
                };
            }
        }

        [DataContract]
        private sealed class RepositoryIndexEntryDto
        {
            [DataMember(Name = "familyFileName", Order = 1)]
            public string FamilyFileName { get; set; }

            [DataMember(Name = "repositoryId", Order = 2)]
            public string RepositoryId { get; set; }
        }

        [DataContract]
        private sealed class RepositoryMetadataDto
        {
            [DataMember(Name = "storageFormatVersion", Order = 1)]
            public int StorageFormatVersion { get; set; }

            [DataMember(Name = "repositoryId", Order = 2)]
            public string RepositoryId { get; set; }

            [DataMember(Name = "familyFileName", Order = 3)]
            public string FamilyFileName { get; set; }

            [DataMember(Name = "createdAt", Order = 4)]
            public string CreatedAt { get; set; }
        }
    }
}
