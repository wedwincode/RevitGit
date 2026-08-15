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

        public Version AddVersion(DateTimeOffset createdAt, string comment)
        {
            var currentVariant = GetVariant(CurrentVariantId);
            var version = new Version(
                NextVersionId(),
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
            var sourceVersion = GetVersion(sourceVersionId);
            var currentVariant = GetVariant(CurrentVariantId);
            var version = new Version(
                NextVersionId(),
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
