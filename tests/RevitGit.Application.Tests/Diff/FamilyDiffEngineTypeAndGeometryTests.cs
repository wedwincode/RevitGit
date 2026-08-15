using System;
using System.Collections.Generic;
using System.Linq;
using RevitGit.Application.Diff;
using RevitGit.Domain.Snapshots;
using Xunit;

namespace RevitGit.Application.Tests.Diff
{
    public sealed class FamilyDiffEngineTypeAndGeometryTests
    {
        private readonly FamilyDiffEngine _engine = new FamilyDiffEngine();

        [Fact]
        public void AddedTypeContainsNewState()
        {
            var before = Snapshot(Parameters(), new FamilyTypeSnapshot[0]);
            var added = Type("1200x2100", Value("family:width", 1200));
            var after = Snapshot(Parameters(), new[] { added });

            var diff = _engine.Compare(before, after);
            var change = Assert.Single(diff.TypeChanges);

            Assert.Equal(ChangeKind.Added, change.Kind);
            Assert.Equal("1200x2100", change.Name);
            Assert.Null(change.Before);
            Assert.Equal(added, change.After);
            Assert.Empty(change.ValueChanges);
            Assert.True(diff.HasChanges);
        }

        [Fact]
        public void RemovedTypeContainsOldState()
        {
            var removed = Type("1200x2100", Value("family:width", 1200));
            var before = Snapshot(Parameters(), new[] { removed });
            var after = Snapshot(Parameters(), new FamilyTypeSnapshot[0]);

            var change = Assert.Single(_engine.Compare(before, after).TypeChanges);

            Assert.Equal(ChangeKind.Removed, change.Kind);
            Assert.Equal(removed, change.Before);
            Assert.Null(change.After);
        }

        [Fact]
        public void ModifiedTypeReportsModifiedParameterValue()
        {
            var before = Snapshot(Parameters(), new[] { Type("Default", Value("family:width", 900)) });
            var after = Snapshot(Parameters(), new[] { Type("Default", Value("family:width", 1000)) });

            var typeChange = Assert.Single(_engine.Compare(before, after).TypeChanges);
            var valueChange = Assert.Single(typeChange.ValueChanges);

            Assert.Equal(ChangeKind.Modified, typeChange.Kind);
            Assert.Equal("family:width", valueChange.ParameterStableKey);
            Assert.Equal("Width", valueChange.ParameterName);
            Assert.Equal(ChangeKind.Modified, valueChange.Kind);
            Assert.Equal(ParameterValue.FromDouble(900), valueChange.Before);
            Assert.Equal(ParameterValue.FromDouble(1000), valueChange.After);
        }

        [Fact]
        public void MultipleValueChangesUseDeterministicParameterNameOrdering()
        {
            var parameters = Parameters(Width(), Height(), Depth());
            var before = Snapshot(parameters, new[]
            {
                Type("Default",
                    Value("family:width", 900),
                    Value("family:height", 2100),
                    Value("family:depth", 100))
            });
            var after = Snapshot(parameters.Reverse(), new[]
            {
                Type("Default",
                    Value("family:depth", 150),
                    Value("family:width", 1000),
                    Value("family:height", 2200))
            });

            var changes = Assert.Single(_engine.Compare(before, after).TypeChanges).ValueChanges;

            Assert.Equal(new[] { "Depth", "Height", "Width" }, changes.Select(x => x.ParameterName));
        }

        [Fact]
        public void TypeValueCanBeAddedAndRemovedWithoutEngineFailure()
        {
            var parameters = Parameters(Width(), Height());
            var before = Snapshot(parameters, new[]
            {
                Type("Default", Value("family:width", 900))
            });
            var after = Snapshot(parameters, new[]
            {
                Type("Default", Value("family:height", 2100))
            });

            var changes = Assert.Single(_engine.Compare(before, after).TypeChanges).ValueChanges;

            Assert.Equal(2, changes.Count);
            Assert.Equal("Height", changes[0].ParameterName);
            Assert.Equal(ChangeKind.Added, changes[0].Kind);
            Assert.Null(changes[0].Before);
            Assert.Equal(ParameterValue.FromDouble(2100), changes[0].After);
            Assert.Equal("Width", changes[1].ParameterName);
            Assert.Equal(ChangeKind.Removed, changes[1].Kind);
            Assert.Equal(ParameterValue.FromDouble(900), changes[1].Before);
            Assert.Null(changes[1].After);
        }

        [Fact]
        public void TypeRenameIsRemovalAndAddition()
        {
            var before = Snapshot(Parameters(), new[] { Type("Default", Value("family:width", 900)) });
            var after = Snapshot(Parameters(), new[] { Type("Standard", Value("family:width", 900)) });

            var changes = _engine.Compare(before, after).TypeChanges;

            Assert.Equal(2, changes.Count);
            Assert.Equal("Default", changes[0].Name);
            Assert.Equal(ChangeKind.Removed, changes[0].Kind);
            Assert.Equal("Standard", changes[1].Name);
            Assert.Equal(ChangeKind.Added, changes[1].Kind);
        }

        [Fact]
        public void TypeChangesUseDeterministicNameOrdering()
        {
            var before = Snapshot(Parameters(), new FamilyTypeSnapshot[0]);
            var after = Snapshot(Parameters(), new[]
            {
                Type("Wide", Value("family:width", 1200)),
                Type("Default", Value("family:width", 900)),
                Type("Narrow", Value("family:width", 700))
            });

            Assert.Equal(
                new[] { "Default", "Narrow", "Wide" },
                _engine.Compare(before, after).TypeChanges.Select(x => x.Name));
        }

        [Theory]
        [InlineData(null, null, false)]
        [InlineData(null, "geometry-a", true)]
        [InlineData("geometry-a", null, true)]
        [InlineData("geometry-a", "geometry-a", false)]
        [InlineData("geometry-a", "geometry-b", true)]
        public void GeometryFingerprintComparisonHasExplicitNullSemantics(
            string beforeFingerprint,
            string afterFingerprint,
            bool expectedChanged)
        {
            var diff = _engine.Compare(
                Snapshot(fingerprint: beforeFingerprint),
                Snapshot(fingerprint: afterFingerprint));

            Assert.Equal(expectedChanged, diff.GeometryChanged);
            Assert.Equal(expectedChanged, diff.HasChanges);
        }

        [Fact]
        public void SnapshotNumericEqualityProducesNoValueDiff()
        {
            var before = Snapshot(Parameters(), new[]
            {
                Type("Default", Value("family:width", 900.00000001))
            });
            var after = Snapshot(Parameters(), new[]
            {
                Type("Default", Value("family:width", 900))
            });

            Assert.False(_engine.Compare(before, after).HasChanges);
        }

        [Fact]
        public void TypeAndValueChangesAreDirectionalAndSymmetric()
        {
            var parameters = Parameters(Width(), Height());
            var first = Snapshot(parameters, new[]
            {
                Type("Default", Value("family:width", 900))
            });
            var second = Snapshot(parameters, new[]
            {
                Type("Default", Value("family:width", 1000), Value("family:height", 2100)),
                Type("Wide", Value("family:width", 1200))
            });

            var forward = _engine.Compare(first, second);
            var reverse = _engine.Compare(second, first);
            var forwardDefault = forward.TypeChanges.Single(x => x.Name == "Default");
            var reverseDefault = reverse.TypeChanges.Single(x => x.Name == "Default");

            Assert.Equal(ChangeKind.Added, forward.TypeChanges.Single(x => x.Name == "Wide").Kind);
            Assert.Equal(ChangeKind.Removed, reverse.TypeChanges.Single(x => x.Name == "Wide").Kind);
            Assert.Equal(ChangeKind.Added, forwardDefault.ValueChanges.Single(x => x.ParameterName == "Height").Kind);
            Assert.Equal(ChangeKind.Removed, reverseDefault.ValueChanges.Single(x => x.ParameterName == "Height").Kind);
            Assert.Equal(
                forwardDefault.ValueChanges.Single(x => x.ParameterName == "Width").Before,
                reverseDefault.ValueChanges.Single(x => x.ParameterName == "Width").After);
            Assert.Equal(
                forwardDefault.ValueChanges.Single(x => x.ParameterName == "Width").After,
                reverseDefault.ValueChanges.Single(x => x.ParameterName == "Width").Before);
        }

        [Fact]
        public void LogicalInputOrderDoesNotChangeDiffOrdering()
        {
            var parameters = Parameters(Width(), Height(), Depth());
            var before = Snapshot(parameters, new FamilyTypeSnapshot[0]);
            var firstAfter = Snapshot(parameters, new[]
            {
                Type("Wide", Value("family:width", 1200)),
                Type("Default", Value("family:width", 900))
            });
            var secondAfter = Snapshot(parameters.Reverse(), new[]
            {
                Type("Default", Value("family:width", 900)),
                Type("Wide", Value("family:width", 1200))
            });

            var first = _engine.Compare(before, firstAfter);
            var second = _engine.Compare(before, secondAfter);

            Assert.Equal(first.TypeChanges.Select(x => x.Name), second.TypeChanges.Select(x => x.Name));
            Assert.Equal(first.TypeChanges.Select(x => x.Kind), second.TypeChanges.Select(x => x.Kind));
        }

        [Fact]
        public void PublishedDiffCollectionsCannotBeMutated()
        {
            var before = Snapshot(Parameters(), new[] { Type("Default", Value("family:width", 900)) });
            var after = Snapshot(Parameters(Width(), Height()), new[]
            {
                Type("Default", Value("family:width", 1000))
            });
            var diff = _engine.Compare(before, after);

            Assert.Throws<NotSupportedException>(() => ((IList<ParameterChange>)diff.ParameterChanges).Clear());
            Assert.Throws<NotSupportedException>(() => ((IList<FamilyTypeChange>)diff.TypeChanges).Clear());
            Assert.Throws<NotSupportedException>(
                () => ((IList<ParameterValueChange>)Assert.Single(diff.TypeChanges).ValueChanges).Clear());
        }

        private static FamilySnapshot Snapshot(
            IEnumerable<FamilyParameterSnapshot> parameters = null,
            IEnumerable<FamilyTypeSnapshot> types = null,
            string fingerprint = null)
        {
            return new FamilySnapshot(
                "Door",
                "Doors",
                parameters ?? new FamilyParameterSnapshot[0],
                types ?? new FamilyTypeSnapshot[0],
                fingerprint);
        }

        private static IEnumerable<FamilyParameterSnapshot> Parameters(
            params FamilyParameterSnapshot[] parameters)
        {
            return parameters.Length == 0 ? new[] { Width() } : parameters;
        }

        private static FamilyParameterSnapshot Width()
        {
            return Parameter("family:width", "Width");
        }

        private static FamilyParameterSnapshot Height()
        {
            return Parameter("family:height", "Height");
        }

        private static FamilyParameterSnapshot Depth()
        {
            return Parameter("family:depth", "Depth");
        }

        private static FamilyParameterSnapshot Parameter(string stableKey, string name)
        {
            return new FamilyParameterSnapshot(
                stableKey, name, ParameterDataType.Length, ParameterScope.Type, null);
        }

        private static FamilyTypeSnapshot Type(
            string name,
            params KeyValuePair<string, ParameterValue>[] values)
        {
            return new FamilyTypeSnapshot(name, values);
        }

        private static KeyValuePair<string, ParameterValue> Value(string stableKey, double value)
        {
            return new KeyValuePair<string, ParameterValue>(stableKey, ParameterValue.FromDouble(value));
        }
    }
}
