using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using RevitGit.Domain.Exceptions;
using RevitGit.Domain.Identifiers;

namespace RevitGit.Domain.History
{
    public sealed class FamilyHistory
    {
        private readonly Dictionary<VersionId, Version> _versions;
        private readonly Dictionary<VariantId, Variant> _variants;

        private FamilyHistory(Version initialVersion, Variant mainVariant)
        {
            _versions = new Dictionary<VersionId, Version>
            {
                { initialVersion.Id, initialVersion }
            };
            _variants = new Dictionary<VariantId, Variant>
            {
                { mainVariant.Id, mainVariant }
            };

            Versions = new ReadOnlyDictionary<VersionId, Version>(_versions);
            Variants = new ReadOnlyDictionary<VariantId, Variant>(_variants);
            CurrentVariantId = mainVariant.Id;
        }

        private FamilyHistory(
            Dictionary<VersionId, Version> versions,
            Dictionary<VariantId, Variant> variants,
            VariantId currentVariantId)
        {
            _versions = versions;
            _variants = variants;
            Versions = new ReadOnlyDictionary<VersionId, Version>(_versions);
            Variants = new ReadOnlyDictionary<VariantId, Variant>(_variants);
            CurrentVariantId = currentVariantId;
        }

        public IReadOnlyDictionary<VersionId, Version> Versions { get; }

        public IReadOnlyDictionary<VariantId, Variant> Variants { get; }

        public VariantId CurrentVariantId { get; private set; }

        public static FamilyHistory Create(
            string mainVariantName,
            DateTimeOffset initialCreatedAt,
            string initialComment)
        {
            var initialVersion = new Version(
                VersionId.New(),
                null,
                initialCreatedAt,
                initialComment,
                null);
            var mainVariant = new Variant(
                VariantId.New(),
                mainVariantName,
                initialVersion.Id);

            return new FamilyHistory(initialVersion, mainVariant);
        }

        public static FamilyHistory Rehydrate(
            IEnumerable<Version> versions,
            IEnumerable<Variant> variants,
            VariantId currentVariantId)
        {
            if (versions == null)
            {
                throw new DomainException("Versions are required to rehydrate family history.");
            }

            if (variants == null)
            {
                throw new DomainException("Variants are required to rehydrate family history.");
            }

            if (currentVariantId == null)
            {
                throw new DomainException("Current variant identifier is required.");
            }

            var versionMap = new Dictionary<VersionId, Version>();
            foreach (var version in versions)
            {
                if (version == null || versionMap.ContainsKey(version.Id))
                {
                    throw new DomainException("Rehydrated history contains a null or duplicate version.");
                }

                versionMap.Add(version.Id, version);
            }

            var variantMap = new Dictionary<VariantId, Variant>();
            foreach (var variant in variants)
            {
                if (variant == null || variantMap.ContainsKey(variant.Id))
                {
                    throw new DomainException("Rehydrated history contains a null or duplicate variant.");
                }

                variantMap.Add(variant.Id, variant);
            }

            if (versionMap.Count == 0 || variantMap.Count == 0 || !variantMap.ContainsKey(currentVariantId))
            {
                throw new DomainException("Rehydrated history is incomplete.");
            }

            foreach (var version in versionMap.Values)
            {
                if ((version.ParentVersionId != null && !versionMap.ContainsKey(version.ParentVersionId))
                    || (version.RestoredFromVersionId != null
                        && !versionMap.ContainsKey(version.RestoredFromVersionId)))
                {
                    throw new DomainException("Rehydrated history contains a dangling version reference.");
                }
            }

            foreach (var variant in variantMap.Values)
            {
                if (!versionMap.ContainsKey(variant.CurrentVersionId))
                {
                    throw new DomainException("Rehydrated history contains a dangling variant reference.");
                }
            }

            return new FamilyHistory(versionMap, variantMap, currentVariantId);
        }

        public Version AddVersion(DateTimeOffset createdAt, string comment)
        {
            return AddVersion(NextVersionId(), createdAt, comment);
        }

        public Version AddVersion(VersionId versionId, DateTimeOffset createdAt, string comment)
        {
            if (versionId == null)
            {
                throw new DomainException("Version identifier is required.");
            }

            if (_versions.ContainsKey(versionId))
            {
                throw new DomainException("Version identifier already exists in family history.");
            }

            var currentVariant = GetVariant(CurrentVariantId);
            var version = new Version(
                versionId,
                currentVariant.CurrentVersionId,
                createdAt,
                comment,
                null);

            _versions.Add(version.Id, version);
            currentVariant.MoveTo(version.Id);

            return version;
        }

        public Variant CreateVariant(VersionId fromVersionId, string name)
        {
            var version = GetVersion(fromVersionId);
            var variant = new Variant(NextVariantId(), name, version.Id);

            _variants.Add(variant.Id, variant);

            return variant;
        }

        public Version AddRestoredVersion(
            VersionId sourceVersionId,
            DateTimeOffset createdAt,
            string comment)
        {
            return AddRestoredVersion(NextVersionId(), sourceVersionId, createdAt, comment);
        }

        public Version AddRestoredVersion(
            VersionId versionId,
            VersionId sourceVersionId,
            DateTimeOffset createdAt,
            string comment)
        {
            if (versionId == null)
            {
                throw new DomainException("Version identifier is required.");
            }

            if (_versions.ContainsKey(versionId))
            {
                throw new DomainException("Version identifier already exists in family history.");
            }

            var sourceVersion = GetVersion(sourceVersionId);
            var currentVariant = GetVariant(CurrentVariantId);
            var version = new Version(
                versionId,
                currentVariant.CurrentVersionId,
                createdAt,
                comment,
                sourceVersion.Id);

            _versions.Add(version.Id, version);
            currentVariant.MoveTo(version.Id);

            return version;
        }

        public void SwitchVariant(VariantId variantId)
        {
            CurrentVariantId = GetVariant(variantId).Id;
        }

        public Version GetVersion(VersionId versionId)
        {
            Version version;
            if (versionId == null || !_versions.TryGetValue(versionId, out version))
            {
                throw new UnknownVersionException(versionId);
            }

            return version;
        }

        public Variant GetVariant(VariantId variantId)
        {
            Variant variant;
            if (variantId == null || !_variants.TryGetValue(variantId, out variant))
            {
                throw new UnknownVariantException(variantId);
            }

            return variant;
        }

        private VersionId NextVersionId()
        {
            VersionId id;
            do
            {
                id = VersionId.New();
            }
            while (_versions.ContainsKey(id));

            return id;
        }

        private VariantId NextVariantId()
        {
            VariantId id;
            do
            {
                id = VariantId.New();
            }
            while (_variants.ContainsKey(id));

            return id;
        }
    }
}
