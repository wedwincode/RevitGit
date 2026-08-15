using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using RevitGit.Application.Abstractions;
using RevitGit.Application.Models;
using RevitGit.Domain.Exceptions;
using RevitGit.Domain.History;
using RevitGit.Domain.Identifiers;
using RevitGit.Infrastructure.FileSystem.Exceptions;
using RevitGit.Infrastructure.FileSystem.IO;
using RevitGit.Infrastructure.FileSystem.Repositories;
using RevitGit.Infrastructure.FileSystem.Serialization;
using HistoryVersion = RevitGit.Domain.History.Version;

namespace RevitGit.Infrastructure.FileSystem.History
{
    public sealed class FileSystemHistoryRepository : IHistoryRepository
    {
        private readonly FamilyRepositoryManager _repositoryManager;
        private readonly StorageJsonSerializer _serializer;
        private readonly AtomicFileWriter _writer;

        public FileSystemHistoryRepository(FamilyRepositoryManager repositoryManager)
        {
            _repositoryManager = repositoryManager ?? throw new ArgumentNullException(nameof(repositoryManager));
            _serializer = new StorageJsonSerializer();
            _writer = new AtomicFileWriter();
        }

        public bool Exists(FamilyIdentity familyIdentity)
        {
            RequireIdentity(familyIdentity);
            if (!_repositoryManager.Exists(familyIdentity.Value))
            {
                return false;
            }

            return File.Exists(_repositoryManager.Open(familyIdentity.Value).Paths.HistoryPath);
        }

        public FamilyHistory Load(FamilyIdentity familyIdentity)
        {
            RequireIdentity(familyIdentity);
            if (!Exists(familyIdentity))
            {
                return null;
            }

            var path = _repositoryManager.Open(familyIdentity.Value).Paths.HistoryPath;
            HistoryDto dto;
            try
            {
                dto = _serializer.Deserialize<HistoryDto>(File.ReadAllBytes(path), "Family history");
            }
            catch (IOException exception)
            {
                throw new StorageException("Family history could not be read.", exception);
            }

            try
            {
                return FromDto(dto);
            }
            catch (Exception exception) when (
                exception is DomainException
                || exception is FormatException
                || exception is ArgumentException
                || exception is NullReferenceException)
            {
                throw new RepositoryCorruptedException(
                    "Family history has an unsupported or inconsistent format.",
                    exception);
            }
        }

        public void Save(FamilyIdentity familyIdentity, FamilyHistory history)
        {
            RequireIdentity(familyIdentity);
            if (history == null)
            {
                throw new ArgumentNullException(nameof(history));
            }

            var repository = _repositoryManager.Exists(familyIdentity.Value)
                ? _repositoryManager.Open(familyIdentity.Value)
                : _repositoryManager.Initialize(familyIdentity.Value);
            _writer.Write(repository.Paths.HistoryPath, _serializer.Serialize(ToDto(history)));
        }

        private static HistoryDto ToDto(FamilyHistory history)
        {
            var versions = history.Versions.Values
                .OrderBy(version => version.CreatedAt)
                .ThenBy(version => version.Id.Value)
                .Select(version => new VersionDto
                {
                    Id = version.Id.Value.ToString("D"),
                    ParentVersionId = ToString(version.ParentVersionId),
                    CreatedAt = version.CreatedAt.ToString("O", CultureInfo.InvariantCulture),
                    Comment = version.Comment,
                    RestoredFromVersionId = ToString(version.RestoredFromVersionId)
                })
                .ToList();
            var variants = history.Variants.Values
                .OrderBy(variant => variant.Name, StringComparer.Ordinal)
                .ThenBy(variant => variant.Id.Value)
                .Select(variant => new VariantDto
                {
                    Id = variant.Id.Value.ToString("D"),
                    Name = variant.Name,
                    CurrentVersionId = variant.CurrentVersionId.Value.ToString("D")
                })
                .ToList();

            return new HistoryDto
            {
                StorageFormatVersion = StorageFormat.CurrentVersion,
                CurrentVariantId = history.CurrentVariantId.Value.ToString("D"),
                Versions = versions,
                Variants = variants
            };
        }

        private static FamilyHistory FromDto(HistoryDto dto)
        {
            if (dto == null
                || dto.StorageFormatVersion != StorageFormat.CurrentVersion
                || dto.Versions == null
                || dto.Variants == null)
            {
                throw new RepositoryCorruptedException("Family history format is incomplete.");
            }

            var versions = new List<HistoryVersion>();
            foreach (var version in dto.Versions)
            {
                DateTimeOffset createdAt;
                if (!DateTimeOffset.TryParseExact(
                    version.CreatedAt,
                    "O",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out createdAt))
                {
                    throw new RepositoryCorruptedException("Family history contains an invalid timestamp.");
                }

                versions.Add(HistoryVersion.Rehydrate(
                    VersionId(version.Id),
                    OptionalVersionId(version.ParentVersionId),
                    createdAt,
                    version.Comment,
                    OptionalVersionId(version.RestoredFromVersionId)));
            }

            var variants = new List<Variant>();
            foreach (var variant in dto.Variants)
            {
                variants.Add(Variant.Rehydrate(
                    VariantId(variant.Id),
                    variant.Name,
                    VersionId(variant.CurrentVersionId)));
            }

            return FamilyHistory.Rehydrate(versions, variants, VariantId(dto.CurrentVariantId));
        }

        private static VersionId VersionId(string value)
        {
            Guid id;
            if (!Guid.TryParse(value, out id))
            {
                throw new RepositoryCorruptedException("Family history contains an invalid version identifier.");
            }

            return new VersionId(id);
        }

        private static VersionId OptionalVersionId(string value)
        {
            return value == null ? null : VersionId(value);
        }

        private static VariantId VariantId(string value)
        {
            Guid id;
            if (!Guid.TryParse(value, out id))
            {
                throw new RepositoryCorruptedException("Family history contains an invalid variant identifier.");
            }

            return new VariantId(id);
        }

        private static string ToString(VersionId id)
        {
            return id == null ? null : id.Value.ToString("D");
        }

        private static void RequireIdentity(FamilyIdentity identity)
        {
            if (identity == null)
            {
                throw new ArgumentNullException(nameof(identity));
            }
        }

        [DataContract]
        private sealed class HistoryDto
        {
            [DataMember(Name = "storageFormatVersion", Order = 1)] public int StorageFormatVersion { get; set; }
            [DataMember(Name = "currentVariantId", Order = 2)] public string CurrentVariantId { get; set; }
            [DataMember(Name = "versions", Order = 3)] public List<VersionDto> Versions { get; set; }
            [DataMember(Name = "variants", Order = 4)] public List<VariantDto> Variants { get; set; }
        }

        [DataContract]
        private sealed class VersionDto
        {
            [DataMember(Name = "id", Order = 1)] public string Id { get; set; }
            [DataMember(Name = "parentVersionId", Order = 2, EmitDefaultValue = false)] public string ParentVersionId { get; set; }
            [DataMember(Name = "createdAt", Order = 3)] public string CreatedAt { get; set; }
            [DataMember(Name = "comment", Order = 4, EmitDefaultValue = false)] public string Comment { get; set; }
            [DataMember(Name = "restoredFromVersionId", Order = 5, EmitDefaultValue = false)] public string RestoredFromVersionId { get; set; }
        }

        [DataContract]
        private sealed class VariantDto
        {
            [DataMember(Name = "id", Order = 1)] public string Id { get; set; }
            [DataMember(Name = "name", Order = 2)] public string Name { get; set; }
            [DataMember(Name = "currentVersionId", Order = 3)] public string CurrentVersionId { get; set; }
        }
    }
}
