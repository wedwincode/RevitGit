using System;
using System.Collections.Generic;
using System.Linq;
using RevitGit.Domain.Exceptions;
using RevitGit.Domain.Snapshots;
using Xunit;

namespace RevitGit.Domain.Tests.Snapshots
{
    public sealed class FamilySnapshotTests
    {
        [Fact]
        public void FamilyNameCategoryAndFingerprintAreNormalized()
        {
            var snapshot = Create(familyName: " Door ", category: " Doors ", geometryFingerprint: " abc123 ");

            Assert.Equal("Door", snapshot.FamilyName);
            Assert.Equal("Doors", snapshot.Category);
            Assert.Equal("abc123", snapshot.GeometryFingerprint);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void FamilyNameCannotBeEmpty(string familyName)
        {
            Assert.Throws<DomainException>(() => Create(familyName: familyName));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void CategoryCannotBeEmpty(string category)
        {
            Assert.Throws<DomainException>(() => Create(category: category));
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void DefinedGeometryFingerprintCannotBeEmpty(string fingerprint)
        {
            Assert.Throws<DomainException>(() => Create(geometryFingerprint: fingerprint));
        }

        [Fact]
        public void NullGeometryFingerprintIsSupported()
        {
            Assert.Null(Create(geometryFingerprint: null).GeometryFingerprint);
        }

        [Fact]
        public void NullCollectionsAreRejected()
        {
            Assert.Throws<DomainException>(
                () => new FamilySnapshot("Door", "Doors", null, EmptyTypes(), null));
            Assert.Throws<DomainException>(
                () => new FamilySnapshot("Door", "Doors", Parameters(), null, null));
        }

        [Fact]
        public void ParametersAndTypesHaveCanonicalOrder()
        {
            var parameters = new[] { Height(), Width(), Depth() };
            var types = new[] { Type("Wide", 1200), Type("Default", 900) };

            var snapshot = new FamilySnapshot("Door", "Doors", parameters, types, null);

            Assert.Equal(new[] { "Depth", "Height", "Width" }, snapshot.Parameters.Select(x => x.Name));
            Assert.Equal(new[] { "Default", "Wide" }, snapshot.Types.Select(x => x.Name));
        }

        [Fact]
        public void CollectionInputOrderDoesNotAffectEqualityOrHashCode()
        {
            var first = new FamilySnapshot(
                "Door", "Doors",
                new[] { Width(), Height(), Depth() },
                new[] { Type("Wide", 1200), Type("Default", 900) },
                "abc");
            var second = new FamilySnapshot(
                "Door", "Doors",
                new[] { Depth(), Width(), Height() },
                new[] { Type("Default", 900), Type("Wide", 1200) },
                "abc");

            Assert.Equal(first, second);
            Assert.Equal(first.GetHashCode(), second.GetHashCode());
        }

        [Fact]
        public void DuplicateParameterStableKeysAreRejected()
        {
            var duplicate = new FamilyParameterSnapshot(
                "family:width", "Other Width", ParameterDataType.Length, ParameterScope.Type, null);

            Assert.Throws<DomainException>(
                () => new FamilySnapshot("Door", "Doors", new[] { Width(), duplicate }, EmptyTypes(), null));
        }

        [Fact]
        public void DuplicateParameterNamesAreRejected()
        {
            var duplicate = new FamilyParameterSnapshot(
                "shared:width", "Width", ParameterDataType.Length, ParameterScope.Type, null);

            Assert.Throws<DomainException>(
                () => new FamilySnapshot("Door", "Doors", new[] { Width(), duplicate }, EmptyTypes(), null));
        }

        [Fact]
        public void DuplicateTypeNamesAreRejected()
        {
            Assert.Throws<DomainException>(
                () => new FamilySnapshot(
                    "Door", "Doors", Parameters(), new[] { Type("Default", 900), Type(" Default ", 1000) }, null));
        }

        [Fact]
        public void NullCollectionItemsAreRejected()
        {
            Assert.Throws<DomainException>(
                () => new FamilySnapshot(
                    "Door", "Doors", new FamilyParameterSnapshot[] { null }, EmptyTypes(), null));
            Assert.Throws<DomainException>(
                () => new FamilySnapshot(
                    "Door", "Doors", Parameters(), new FamilyTypeSnapshot[] { null }, null));
        }

        [Fact]
        public void UnknownTypeValueParameterKeyIsRejected()
        {
            var type = new FamilyTypeSnapshot(
                "Default",
                new Dictionary<string, ParameterValue>
                {
                    { "family:unknown", ParameterValue.FromInteger(1) }
                });

            Assert.Throws<DomainException>(
                () => new FamilySnapshot("Door", "Doors", Parameters(), new[] { type }, null));
        }

        [Fact]
        public void MutableInputCollectionsCannotChangeSnapshot()
        {
            var parameters = new List<FamilyParameterSnapshot> { Width() };
            var types = new List<FamilyTypeSnapshot> { Type("Default", 900) };
            var snapshot = new FamilySnapshot("Door", "Doors", parameters, types, null);

            parameters.Clear();
            types.Clear();

            Assert.Single(snapshot.Parameters);
            Assert.Single(snapshot.Types);
            Assert.Throws<NotSupportedException>(
                () => ((IList<FamilyParameterSnapshot>)snapshot.Parameters).Clear());
            Assert.Throws<NotSupportedException>(
                () => ((IList<FamilyTypeSnapshot>)snapshot.Types).Clear());
        }

        [Fact]
        public void CurrentSchemaVersionIsAssigned()
        {
            Assert.Equal(SnapshotSchema.CurrentVersion, Create().SchemaVersion);
            Assert.Equal(1, Create().SchemaVersion);
        }

        [Fact]
        public void DifferentFamilyNameIsNotEqual()
        {
            Assert.NotEqual(Create(familyName: "Door"), Create(familyName: "door"));
        }

        [Fact]
        public void DifferentCategoryIsNotEqual()
        {
            Assert.NotEqual(Create(category: "Doors"), Create(category: "Windows"));
        }

        [Fact]
        public void DifferentParameterIsNotEqual()
        {
            Assert.NotEqual(Create(), Create(parameters: new[] { Width(), Height() }));
        }

        [Fact]
        public void DifferentFormulaIsNotEqual()
        {
            var withFormula = new FamilyParameterSnapshot(
                "family:width", "Width", ParameterDataType.Length, ParameterScope.Type, "Height / 2");

            Assert.NotEqual(Create(), Create(parameters: new[] { withFormula }));
        }

        [Fact]
        public void DifferentTypeValueIsNotEqual()
        {
            Assert.NotEqual(Create(types: new[] { Type("Default", 900) }), Create(types: new[] { Type("Default", 1000) }));
        }

        [Fact]
        public void DifferentGeometryFingerprintIsNotEqual()
        {
            Assert.NotEqual(Create(geometryFingerprint: "abc"), Create(geometryFingerprint: "xyz"));
        }

        [Fact]
        public void SameNullOrSameDefinedFingerprintProducesEquality()
        {
            Assert.Equal(Create(geometryFingerprint: null), Create(geometryFingerprint: null));
            Assert.Equal(Create(geometryFingerprint: "abc"), Create(geometryFingerprint: "abc"));
        }

        private static FamilySnapshot Create(
            string familyName = "Door",
            string category = "Doors",
            IEnumerable<FamilyParameterSnapshot> parameters = null,
            IEnumerable<FamilyTypeSnapshot> types = null,
            string geometryFingerprint = null)
        {
            return new FamilySnapshot(
                familyName,
                category,
                parameters ?? Parameters(),
                types ?? new[] { Type("Default", 900) },
                geometryFingerprint);
        }

        private static IEnumerable<FamilyParameterSnapshot> Parameters()
        {
            return new[] { Width() };
        }

        private static IEnumerable<FamilyTypeSnapshot> EmptyTypes()
        {
            return new FamilyTypeSnapshot[0];
        }

        private static FamilyParameterSnapshot Width()
        {
            return new FamilyParameterSnapshot(
                "family:width", "Width", ParameterDataType.Length, ParameterScope.Type, null);
        }

        private static FamilyParameterSnapshot Height()
        {
            return new FamilyParameterSnapshot(
                "family:height", "Height", ParameterDataType.Length, ParameterScope.Type, null);
        }

        private static FamilyParameterSnapshot Depth()
        {
            return new FamilyParameterSnapshot(
                "family:depth", "Depth", ParameterDataType.Length, ParameterScope.Type, null);
        }

        private static FamilyTypeSnapshot Type(string name, double width)
        {
            return new FamilyTypeSnapshot(
                name,
                new Dictionary<string, ParameterValue>
                {
                    { "family:width", ParameterValue.FromDouble(width) }
                });
        }
    }
}
