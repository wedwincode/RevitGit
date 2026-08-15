using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Security.Cryptography;
using LibGit2Sharp;
using RevitGit.Application.Abstractions;
using RevitGit.Application.Models;
using RevitGit.Domain.History;
using RevitGit.Domain.Identifiers;
using RevitGit.Domain.Snapshots;
using HistoryVersion = RevitGit.Domain.History.Version;

namespace RevitGit.Infrastructure.Git
{
    public sealed class LibGit2VersionRepository : IHistoryRepository, IVersionContentStore, IVersionSnapshotStore, IPreparedRestoreContentStore
    {
        private const string FamilyFileName = "family.rfa";
        private const string SnapshotFileName = "snapshot.json";
        private const string VersionFileName = "version.json";
        private static readonly string[] ControlledFiles = { FamilyFileName, SnapshotFileName, VersionFileName };
        private readonly string _repositoryDirectory;
        private readonly string _workDirectory;
        private readonly IHistoryRepository _metadataRepository;
        private readonly Func<byte[]> _snapshotContentProvider;
        private readonly Func<byte[], FamilySnapshot> _snapshotDeserializer;
        private PendingVersion _pending;

        public LibGit2VersionRepository(
            string repositoryDirectory,
            IHistoryRepository metadataRepository,
            Func<byte[]> snapshotContentProvider,
            Func<byte[], FamilySnapshot> snapshotDeserializer = null)
        {
            if (string.IsNullOrWhiteSpace(repositoryDirectory))
                throw new ArgumentException("Repository directory is required.", nameof(repositoryDirectory));
            _repositoryDirectory = Path.GetFullPath(repositoryDirectory);
            _workDirectory = Path.Combine(_repositoryDirectory, "repo");
            _metadataRepository = metadataRepository ?? throw new ArgumentNullException(nameof(metadataRepository));
            _snapshotContentProvider = snapshotContentProvider ?? throw new ArgumentNullException(nameof(snapshotContentProvider));
            _snapshotDeserializer = snapshotDeserializer;
            CleanupAbandonedPendingDirectories();
        }

        public int CommitCount
        {
            get
            {
                if (!Repository.IsValid(_workDirectory)) return 0;
                using (var repository = OpenRepository()) return EnumerateCommits(repository).Count;
            }
        }

        public string EmbeddedGitAssemblyVersion => typeof(Repository).Assembly.GetName().Version.ToString();

        public string CurrentInternalBranchName
        {
            get
            {
                using (var repository = OpenRepository())
                {
                    if (repository.Info.IsHeadDetached)
                        throw new GitRepositoryCorruptedException("The current variant is not attached to an internal branch.");
                    return repository.Head.FriendlyName;
                }
            }
        }

        public bool Exists(FamilyIdentity familyIdentity)
        {
            RequireIdentity(familyIdentity);
            return _metadataRepository.Exists(familyIdentity);
        }

        public FamilyHistory Load(FamilyIdentity familyIdentity)
        {
            RequireIdentity(familyIdentity);
            var history = _metadataRepository.Load(familyIdentity);
            if (history == null) return null;
            ValidateIntegrity(history);
            return history;
        }

        public void Save(FamilyIdentity familyIdentity, FamilyHistory history)
        {
            RequireIdentity(familyIdentity);
            if (history == null) throw new ArgumentNullException(nameof(history));

            var previous = _metadataRepository.Exists(familyIdentity)
                ? _metadataRepository.Load(familyIdentity)
                : null;
            var newVersions = history.Versions.Values
                .Where(version => previous == null || !previous.Versions.ContainsKey(version.Id))
                .ToList();

            if (newVersions.Count > 1)
                throw new GitStorageException("More than one version cannot be persisted in one operation.");
            if (newVersions.Count == 1 && (_pending == null || !_pending.VersionId.Equals(newVersions[0].Id)))
                throw new GitStorageException("The new version content was not prepared.");
            if (newVersions.Count == 0 && _pending != null)
                throw new GitStorageException("Prepared content does not correspond to a new version.");

            try
            {
                if (newVersions.Count == 1)
                {
                    EnsureRepository();
                    CommitVersion(history, newVersions[0]);
                }

                if (Repository.IsValid(_workDirectory))
                {
                    using (var repository = OpenRepository())
                    {
                        SynchronizeVariants(repository, history);
                    }
                }

                _metadataRepository.Save(familyIdentity, history);
                CleanupPending();
            }
            catch (GitStorageException)
            {
                throw;
            }
            catch (LibGit2SharpException exception)
            {
                throw new GitStorageException("Version history could not be persisted.", exception);
            }
        }

        public void StoreCurrentVersion(FamilyIdentity familyIdentity, VersionId versionId)
        {
            RequireIdentity(familyIdentity);
            if (versionId == null) throw new ArgumentNullException(nameof(versionId));
            if (_pending != null) throw new GitStorageException("Another version is already being prepared.");
            if (Repository.IsValid(_workDirectory) && TryGetStorageObjectId(versionId, out _))
                throw new GitStorageException("The version identifier is already stored.");

            byte[] snapshot;
            try
            {
                snapshot = _snapshotContentProvider();
            }
            catch (Exception exception)
            {
                throw new GitStorageException("The version snapshot could not be captured.", exception);
            }

            if (snapshot == null) throw new GitStorageException("The version snapshot is missing.");
            var pendingDirectory = Path.Combine(_repositoryDirectory, ".pending-" + versionId.Value.ToString("N"));
            try
            {
                var familyContent = ReadAllBytesShared(familyIdentity.Value);
                Directory.CreateDirectory(_repositoryDirectory);
                Directory.CreateDirectory(pendingDirectory);
                File.WriteAllBytes(Path.Combine(pendingDirectory, FamilyFileName), familyContent);
                File.WriteAllBytes(Path.Combine(pendingDirectory, SnapshotFileName), snapshot);
                _pending = new PendingVersion(versionId, pendingDirectory);
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                TryDeleteDirectory(pendingDirectory);
                throw new GitStorageException("The version content could not be prepared.", exception);
            }
        }

        public void RestoreVersionContent(FamilyIdentity familyIdentity, VersionId versionId)
        {
            RequireIdentity(familyIdentity);
            var content = ReadVersionFile(versionId, FamilyFileName);
            var temporaryPath = familyIdentity.Value + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                File.WriteAllBytes(temporaryPath, content);
                File.Replace(temporaryPath, familyIdentity.Value, null);
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                TryDelete(temporaryPath);
                throw new GitStorageException("The selected version content could not be restored.", exception);
            }
        }

        public PreparedRestoreContent PrepareRestoreContent(
            FamilyIdentity familyIdentity,
            VersionId sourceVersionId,
            VersionId expectedCurrentVersionId)
        {
            RequireIdentity(familyIdentity);
            if ((File.GetAttributes(familyIdentity.Value) & FileAttributes.ReadOnly) != 0)
                throw new UnauthorizedAccessException("The family file is read-only.");
            var familyContent = ReadVersionFile(sourceVersionId, FamilyFileName);
            var snapshotContent = ReadVersionFile(sourceVersionId, SnapshotFileName);
            // The current version is read as part of preflight so a rollback source is known to exist.
            ReadVersionFile(expectedCurrentVersionId, FamilyFileName);
            ReadVersionFile(expectedCurrentVersionId, SnapshotFileName);
            FamilySnapshot snapshot = null;
            if (_snapshotDeserializer != null)
            {
                try { snapshot = _snapshotDeserializer(snapshotContent); }
                catch (Exception exception)
                {
                    throw new GitRepositoryCorruptedException("The stored version snapshot is invalid.", exception);
                }
            }
            var stagingDirectory = Path.Combine(_repositoryDirectory, ".tmp-restore-" + Guid.NewGuid().ToString("N"));
            var stagingPath = Path.Combine(stagingDirectory, FamilyFileName);
            try
            {
                Directory.CreateDirectory(stagingDirectory);
                File.WriteAllBytes(stagingPath, familyContent);
                return new PreparedRestoreContent(familyIdentity, sourceVersionId, expectedCurrentVersionId,
                    stagingPath, snapshot, ComputeSha256(familyContent));
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                TryDeleteDirectory(stagingDirectory);
                throw new GitStorageException("The selected version could not be prepared.", exception);
            }
        }

        public void PublishPreparedRestore(FamilyIdentity familyIdentity, PreparedRestoreContent prepared)
        {
            RequirePrepared(familyIdentity, prepared);
            // Revit has the staged file open at this point. Read the same immutable source blob
            // from the repository instead of competing with Revit's file handle.
            var content = ReadVersionFile(prepared.SourceVersionId, FamilyFileName);
            if (!string.Equals(ComputeSha256(content), prepared.BinaryChecksum, StringComparison.Ordinal))
                throw new GitRepositoryCorruptedException("Prepared restore content failed checksum verification.");
            var temporaryPath = familyIdentity.Value + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                File.WriteAllBytes(temporaryPath, content);
                File.Replace(temporaryPath, familyIdentity.Value, null);
            }
            finally { TryDelete(temporaryPath); }
        }

        public void RollbackPreparedRestore(FamilyIdentity familyIdentity, PreparedRestoreContent prepared)
        {
            RequirePrepared(familyIdentity, prepared);
            PublishVersionFile(familyIdentity, prepared.ExpectedCurrentVersionId);
        }

        public void StorePreparedRestore(
            FamilyIdentity familyIdentity,
            PreparedRestoreContent prepared,
            VersionId restoredVersionId)
        {
            RequirePrepared(familyIdentity, prepared);
            if (_pending != null) throw new GitStorageException("Another version is already being prepared.");
            var pendingDirectory = Path.Combine(_repositoryDirectory, ".pending-" + restoredVersionId.Value.ToString("N"));
            try
            {
                Directory.CreateDirectory(pendingDirectory);
                File.WriteAllBytes(Path.Combine(pendingDirectory, FamilyFileName),
                    ReadVersionFile(prepared.SourceVersionId, FamilyFileName));
                File.WriteAllBytes(Path.Combine(pendingDirectory, SnapshotFileName),
                    ReadVersionFile(prepared.SourceVersionId, SnapshotFileName));
                _pending = new PendingVersion(restoredVersionId, pendingDirectory);
            }
            catch
            {
                TryDeleteDirectory(pendingDirectory);
                throw;
            }
        }

        public void CleanupPreparedRestore(PreparedRestoreContent prepared)
        {
            if (prepared == null) return;
            TryDeleteDirectory(Path.GetDirectoryName(prepared.PreparedFamilyFilePath));
        }

        public FamilySnapshot ReadSnapshot(FamilyIdentity familyIdentity, VersionId versionId)
        {
            RequireIdentity(familyIdentity);
            if (_snapshotDeserializer == null)
                throw new GitStorageException("A snapshot reader is not configured.");
            try
            {
                return _snapshotDeserializer(ReadVersionFile(versionId, SnapshotFileName));
            }
            catch (GitStorageException) { throw; }
            catch (Exception exception)
            {
                throw new GitRepositoryCorruptedException("The stored version snapshot is invalid.", exception);
            }
        }

        public byte[] ReadVersionFile(VersionId versionId, string path)
        {
            if (versionId == null) throw new ArgumentNullException(nameof(versionId));
            if (!ControlledFiles.Contains(path, StringComparer.Ordinal))
                throw new ArgumentException("Only controlled version files can be read.", nameof(path));
            try
            {
                using (var repository = OpenRepository())
                {
                    var commit = ResolveCommit(repository, versionId);
                    var entry = commit.Tree[path];
                    var blob = entry == null ? null : entry.Target as Blob;
                    if (blob == null)
                        throw new GitRepositoryCorruptedException("A controlled file is missing from the stored version.");
                    using (var source = blob.GetContentStream())
                    using (var destination = new MemoryStream())
                    {
                        source.CopyTo(destination);
                        return destination.ToArray();
                    }
                }
            }
            catch (GitStorageException) { throw; }
            catch (LibGit2SharpException exception)
            {
                throw new GitStorageException("The stored version file could not be read.", exception);
            }
        }

        public string GetStorageObjectId(VersionId versionId)
        {
            using (var repository = OpenRepository()) return ResolveCommit(repository, versionId).Id.Sha;
        }

        public VersionId GetParentVersionId(VersionId versionId)
        {
            using (var repository = OpenRepository())
            {
                var commit = ResolveCommit(repository, versionId);
                var parent = commit.Parents.SingleOrDefault();
                return parent == null ? null : ReadMetadata(parent).VersionId;
            }
        }

        public string GetInternalBranchName(VariantId variantId)
        {
            return GitRefNameMapper.ForVariant(variantId);
        }

        public void ValidateIntegrity(FamilyHistory history)
        {
            var result = InspectIntegrity(history);
            if (!result.IsValid)
                throw new GitRepositoryCorruptedException(result.Issues[0].Message);
        }

        public RepositoryValidationResult InspectIntegrity(FamilyHistory history)
        {
            if (history == null) throw new ArgumentNullException(nameof(history));
            var issues = new List<RepositoryValidationIssue>();
            if (!Repository.IsValid(_workDirectory))
            {
                issues.Add(new RepositoryValidationIssue("GIT_REPOSITORY_MISSING", "The internal version repository is missing."));
                return new RepositoryValidationResult(issues);
            }

            try
            {
                using (var repository = OpenRepository())
                {
                    if (repository.Info.IsHeadDetached)
                        issues.Add(new RepositoryValidationIssue("HEAD_DETACHED", "The current variant is detached from its internal branch."));

                    Dictionary<VersionId, Commit> mapping;
                    try { mapping = BuildVersionMap(repository); }
                    catch (GitStorageException exception)
                    {
                        issues.Add(new RepositoryValidationIssue("VERSION_MAPPING_INVALID", exception.Message));
                        return new RepositoryValidationResult(issues);
                    }

                    foreach (var version in history.Versions.Values)
                    {
                        Commit commit;
                        if (!mapping.TryGetValue(version.Id, out commit))
                        {
                            issues.Add(new RepositoryValidationIssue("VERSION_MAPPING_MISSING", "A stored version mapping is missing.", version.Id));
                            continue;
                        }

                        VersionMetadata metadata;
                        try { metadata = ReadMetadata(commit); }
                        catch (GitStorageException exception)
                        {
                            issues.Add(new RepositoryValidationIssue("VERSION_METADATA_INVALID", exception.Message, version.Id));
                            continue;
                        }

                        if (!Equals(metadata.VersionId, version.Id)
                            || !Equals(metadata.ParentVersionId, version.ParentVersionId)
                            || !Equals(metadata.RestoredFromVersionId, version.RestoredFromVersionId)
                            || metadata.CreatedAt != version.CreatedAt
                            || !string.Equals(metadata.Comment, version.Comment, StringComparison.Ordinal))
                            issues.Add(new RepositoryValidationIssue("VERSION_METADATA_MISMATCH", "Stored version metadata does not match family history.", version.Id));

                        var parents = commit.Parents.ToList();
                        if (parents.Count > 1)
                        {
                            issues.Add(new RepositoryValidationIssue("MULTIPLE_PARENTS", "A stored version has more than one parent.", version.Id));
                        }
                        else
                        {
                            var actualParent = parents.Count == 0 ? null : ReadMetadata(parents[0]).VersionId;
                            if (!Equals(actualParent, version.ParentVersionId))
                                issues.Add(new RepositoryValidationIssue("PARENT_MISMATCH", "Stored version parent relationship is inconsistent.", version.Id));
                        }
                    }

                    if (mapping.Count != history.Versions.Count)
                        issues.Add(new RepositoryValidationIssue("UNKNOWN_GIT_VERSIONS", "The internal repository contains versions absent from metadata."));

                    foreach (var variant in history.Variants.Values)
                    {
                        var branch = repository.Branches[GitRefNameMapper.ForVariant(variant.Id)];
                        if (branch == null)
                        {
                            issues.Add(new RepositoryValidationIssue("VARIANT_BRANCH_MISSING", "A mapped variant branch is missing.", null, variant.Id));
                        }
                        else if (!Equals(ReadMetadata(branch.Tip).VersionId, variant.CurrentVersionId))
                        {
                            issues.Add(new RepositoryValidationIssue("VARIANT_TIP_MISMATCH", "A variant tip does not match family history.", variant.CurrentVersionId, variant.Id));
                        }
                    }

                    if (!repository.Info.IsHeadDetached && !string.Equals(repository.Head.FriendlyName,
                        GitRefNameMapper.ForVariant(history.CurrentVariantId), StringComparison.Ordinal))
                        issues.Add(new RepositoryValidationIssue("CURRENT_VARIANT_MISMATCH", "The current variant does not match the attached internal branch.", null, history.CurrentVariantId));
                }
            }
            catch (GitStorageException exception)
            {
                issues.Add(new RepositoryValidationIssue("GIT_VALIDATION_FAILED", exception.Message));
            }
            catch (LibGit2SharpException exception)
            {
                issues.Add(new RepositoryValidationIssue("GIT_VALIDATION_FAILED", "The internal version repository could not be validated: " + exception.Message));
            }

            return new RepositoryValidationResult(issues);
        }

        private void CommitVersion(FamilyHistory history, HistoryVersion version)
        {
            using (var repository = OpenRepository())
            {
                var hasCommits = EnumerateCommits(repository).Count != 0;
                if (hasCommits)
                {
                    var currentBranchName = GitRefNameMapper.ForVariant(history.CurrentVariantId);
                    var currentBranch = repository.Branches[currentBranchName];
                    if (currentBranch == null)
                        throw new GitRepositoryCorruptedException("The current variant branch is missing.");
                    Commands.Checkout(repository, currentBranch, ForceCheckout());
                    var actualParent = ReadMetadata(currentBranch.Tip).VersionId;
                    if (!Equals(actualParent, version.ParentVersionId))
                        throw new GitRepositoryCorruptedException("The new version parent does not match the current variant tip.");
                }
                else if (version.ParentVersionId != null)
                {
                    throw new GitRepositoryCorruptedException("The first stored version cannot have a parent.");
                }

                CopyPendingToWorkTree();
                File.WriteAllBytes(Path.Combine(_workDirectory, VersionFileName), SerializeMetadata(version));
                foreach (var controlledFile in ControlledFiles) Commands.Stage(repository, controlledFile);
                var signature = new Signature("RevitGit", "local@revitgit.invalid", version.CreatedAt);
                var commit = repository.Commit(string.IsNullOrWhiteSpace(version.Comment) ? "Save version" : version.Comment,
                    signature, signature);

                if (!hasCommits)
                {
                    var initialBranchName = repository.Head.FriendlyName;
                    var desiredBranchName = GitRefNameMapper.ForVariant(history.CurrentVariantId);
                    var desiredBranch = repository.Branches.Add(desiredBranchName, commit);
                    Commands.Checkout(repository, desiredBranch, ForceCheckout());
                    if (!string.Equals(initialBranchName, desiredBranchName, StringComparison.Ordinal)
                        && repository.Branches[initialBranchName] != null)
                        repository.Branches.Remove(initialBranchName);
                }
            }
        }

        private static void SynchronizeVariants(Repository repository, FamilyHistory history)
        {
            var mapping = BuildVersionMap(repository);
            foreach (var variant in history.Variants.Values)
            {
                Commit expectedTip;
                if (!mapping.TryGetValue(variant.CurrentVersionId, out expectedTip))
                    throw new GitRepositoryCorruptedException("A variant references a version absent from internal storage.");
                var branchName = GitRefNameMapper.ForVariant(variant.Id);
                var branch = repository.Branches[branchName];
                if (branch == null)
                {
                    repository.Branches.Add(branchName, expectedTip);
                }
                else if (branch.Tip.Id != expectedTip.Id)
                {
                    throw new GitRepositoryCorruptedException("A variant branch unexpectedly points to another version.");
                }
            }

            var current = repository.Branches[GitRefNameMapper.ForVariant(history.CurrentVariantId)];
            if (current == null) throw new GitRepositoryCorruptedException("The current variant branch is missing.");
            Commands.Checkout(repository, current, ForceCheckout());
        }

        private void EnsureRepository()
        {
            if (Repository.IsValid(_workDirectory)) return;
            Directory.CreateDirectory(_workDirectory);
            Repository.Init(_workDirectory);
        }

        private Repository OpenRepository()
        {
            if (!Repository.IsValid(_workDirectory))
                throw new GitRepositoryCorruptedException("The internal version repository is missing or invalid.");
            try { return new Repository(_workDirectory); }
            catch (LibGit2SharpException exception)
            {
                throw new GitRepositoryCorruptedException("The internal version repository could not be opened.", exception);
            }
        }

        private void CopyPendingToWorkTree()
        {
            foreach (var file in new[] { FamilyFileName, SnapshotFileName })
                File.Copy(Path.Combine(_pending.Directory, file), Path.Combine(_workDirectory, file), true);
        }

        private static Dictionary<VersionId, Commit> BuildVersionMap(Repository repository)
        {
            var result = new Dictionary<VersionId, Commit>();
            foreach (var commit in EnumerateCommits(repository))
            {
                var metadata = ReadMetadata(commit);
                if (result.ContainsKey(metadata.VersionId))
                    throw new GitRepositoryCorruptedException("A version identifier is mapped to more than one stored version.");
                result.Add(metadata.VersionId, commit);
            }
            return result;
        }

        private static List<Commit> EnumerateCommits(Repository repository)
        {
            var result = new List<Commit>();
            var visited = new HashSet<string>(StringComparer.Ordinal);
            var stack = new Stack<Commit>(repository.Branches.Where(branch => branch.Tip != null).Select(branch => branch.Tip));
            if (repository.Head != null && repository.Head.Tip != null) stack.Push(repository.Head.Tip);
            while (stack.Count != 0)
            {
                var commit = stack.Pop();
                if (!visited.Add(commit.Id.Sha)) continue;
                result.Add(commit);
                foreach (var parent in commit.Parents) stack.Push(parent);
            }
            return result;
        }

        private Commit ResolveCommit(Repository repository, VersionId versionId)
        {
            Commit commit;
            if (!BuildVersionMap(repository).TryGetValue(versionId, out commit))
                throw new GitRepositoryCorruptedException("The requested version mapping is missing.");
            return commit;
        }

        private bool TryGetStorageObjectId(VersionId versionId, out string objectId)
        {
            using (var repository = OpenRepository())
            {
                Commit commit;
                if (BuildVersionMap(repository).TryGetValue(versionId, out commit))
                {
                    objectId = commit.Id.Sha;
                    return true;
                }
                objectId = null;
                return false;
            }
        }

        private static VersionMetadata ReadMetadata(Commit commit)
        {
            var entry = commit.Tree[VersionFileName];
            var blob = entry == null ? null : entry.Target as Blob;
            if (blob == null) throw new GitRepositoryCorruptedException("Stored version metadata is missing.");
            try
            {
                VersionMetadataDto dto;
                using (var stream = blob.GetContentStream())
                {
                    dto = (VersionMetadataDto)new DataContractJsonSerializer(typeof(VersionMetadataDto)).ReadObject(stream);
                }
                Guid id;
                DateTimeOffset createdAt;
                if (dto == null || dto.SchemaVersion != 1 || !Guid.TryParse(dto.VersionId, out id)
                    || !DateTimeOffset.TryParseExact(dto.CreatedAt, "O", CultureInfo.InvariantCulture,
                        DateTimeStyles.RoundtripKind, out createdAt))
                    throw new GitRepositoryCorruptedException("Stored version metadata has an unsupported format.");
                return new VersionMetadata(new VersionId(id), ParseOptionalId(dto.ParentVersionId), createdAt,
                    string.IsNullOrWhiteSpace(dto.Comment) ? null : dto.Comment, ParseOptionalId(dto.RestoredFromVersionId));
            }
            catch (GitStorageException) { throw; }
            catch (Exception exception) when (exception is SerializationException || exception is FormatException || exception is ArgumentException)
            {
                throw new GitRepositoryCorruptedException("Stored version metadata is invalid.", exception);
            }
        }

        private static byte[] SerializeMetadata(HistoryVersion version)
        {
            var dto = new VersionMetadataDto
            {
                SchemaVersion = 1,
                VersionId = version.Id.Value.ToString("D"),
                ParentVersionId = FormatOptionalId(version.ParentVersionId),
                CreatedAt = version.CreatedAt.ToString("O", CultureInfo.InvariantCulture),
                Comment = version.Comment,
                RestoredFromVersionId = FormatOptionalId(version.RestoredFromVersionId)
            };
            using (var stream = new MemoryStream())
            {
                new DataContractJsonSerializer(typeof(VersionMetadataDto)).WriteObject(stream, dto);
                return stream.ToArray();
            }
        }

        private static VersionId ParseOptionalId(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            Guid id;
            if (!Guid.TryParse(value, out id) || id == Guid.Empty)
                throw new GitRepositoryCorruptedException("Stored version metadata contains an invalid identifier.");
            return new VersionId(id);
        }

        private static string FormatOptionalId(VersionId value)
        {
            return value == null ? null : value.Value.ToString("D");
        }

        private static CheckoutOptions ForceCheckout()
        {
            return new CheckoutOptions { CheckoutModifiers = CheckoutModifiers.Force };
        }

        private static void RequireIdentity(FamilyIdentity identity)
        {
            if (identity == null) throw new ArgumentNullException(nameof(identity));
            if (!File.Exists(identity.Value)) throw new ArgumentException("Family file does not exist.", nameof(identity));
        }

        private static void RequirePrepared(FamilyIdentity identity, PreparedRestoreContent prepared)
        {
            RequireIdentity(identity);
            if (prepared == null || !identity.Equals(prepared.FamilyIdentity))
                throw new ArgumentException("Prepared restore belongs to another family.", nameof(prepared));
        }

        private void PublishVersionFile(FamilyIdentity identity, VersionId versionId)
        {
            var content = ReadVersionFile(versionId, FamilyFileName);
            var temporaryPath = identity.Value + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                File.WriteAllBytes(temporaryPath, content);
                File.Replace(temporaryPath, identity.Value, null);
            }
            finally { TryDelete(temporaryPath); }
        }

        private static string ComputeSha256(byte[] content)
        {
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(content)).Replace("-", string.Empty).ToLowerInvariant();
        }

        private void CleanupPending()
        {
            if (_pending == null) return;
            TryDeleteDirectory(_pending.Directory);
            _pending = null;
        }

        private void CleanupAbandonedPendingDirectories()
        {
            if (!Directory.Exists(_repositoryDirectory)) return;
            try
            {
                foreach (var directory in Directory.GetDirectories(
                    _repositoryDirectory,
                    ".pending-*",
                    SearchOption.TopDirectoryOnly))
                {
                    Directory.Delete(directory, true);
                }
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                throw new GitStorageException("Abandoned version content could not be cleaned.", exception);
            }
        }

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }

        private static byte[] ReadAllBytesShared(string path)
        {
            // Revit keeps a write-capable handle to the active family after Document.Save().
            // We only read, but must keep sharing write/delete access so that handle remains valid.
            using (var source = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete))
            using (var destination = new MemoryStream())
            {
                source.CopyTo(destination);
                return destination.ToArray();
            }
        }

        private static void TryDeleteDirectory(string path)
        {
            try { if (Directory.Exists(path)) Directory.Delete(path, true); } catch { }
        }

        private sealed class PendingVersion
        {
            public PendingVersion(VersionId versionId, string directory)
            {
                VersionId = versionId;
                Directory = directory;
            }
            public VersionId VersionId { get; }
            public string Directory { get; }
        }

        private sealed class VersionMetadata
        {
            public VersionMetadata(VersionId versionId, VersionId parentVersionId, DateTimeOffset createdAt,
                string comment, VersionId restoredFromVersionId)
            {
                VersionId = versionId;
                ParentVersionId = parentVersionId;
                CreatedAt = createdAt;
                Comment = comment;
                RestoredFromVersionId = restoredFromVersionId;
            }
            public VersionId VersionId { get; }
            public VersionId ParentVersionId { get; }
            public DateTimeOffset CreatedAt { get; }
            public string Comment { get; }
            public VersionId RestoredFromVersionId { get; }
        }

        [DataContract]
        private sealed class VersionMetadataDto
        {
            [DataMember(Name = "schemaVersion", Order = 1)] public int SchemaVersion { get; set; }
            [DataMember(Name = "versionId", Order = 2)] public string VersionId { get; set; }
            [DataMember(Name = "parentVersionId", Order = 3, EmitDefaultValue = false)] public string ParentVersionId { get; set; }
            [DataMember(Name = "createdAt", Order = 4)] public string CreatedAt { get; set; }
            [DataMember(Name = "comment", Order = 5, EmitDefaultValue = false)] public string Comment { get; set; }
            [DataMember(Name = "restoredFromVersionId", Order = 6, EmitDefaultValue = false)] public string RestoredFromVersionId { get; set; }
        }
    }
}
