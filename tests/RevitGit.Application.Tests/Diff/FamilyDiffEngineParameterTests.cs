using System;
using System.Collections.Generic;
using System.Linq;
using RevitGit.Application.Diff;
using RevitGit.Domain.Snapshots;
using Xunit;

namespace RevitGit.Application.Tests.Diff
{
    public sealed class FamilyDiffEngineParameterTests
    {
        private readonly FamilyDiffEngine _engine = new FamilyDiffEngine();

        [Fact]
        public void IdenticalSnapshotsProduceEmptyDiff()
        {
            var before = Snapshot(parameters: new[] { Width() }, fingerprint: "geometry-a");
            var after = Snapshot(parameters: new[] { Width() }, fingerprint: "geometry-a");

            var diff = _engine.Compare(before, after);

            Assert.False(diff.HasChanges);
            Assert.Null(diff.FamilyNameChange);
            Assert.Null(diff.CategoryChange);
            Assert.Empty(diff.ParameterChanges);
            Assert.Empty(diff.TypeChanges);
            Assert.False(diff.GeometryChanged);
        }

        [Fact]
        public void FamilyNameChangeContainsOldAndNewValues()
        {
            var diff = _engine.Compare(Snapshot(familyName: "Door"), Snapshot(familyName: "Door_Main"));

            Assert.Equal("Door", diff.FamilyNameChange.OldValue);
            Assert.Equal("Door_Main", diff.FamilyNameChange.NewValue);
            Assert.True(diff.HasChanges);
        }

        [Fact]
        public void CategoryChangeContainsOldAndNewValues()
        {
            var diff = _engine.Compare(Snapshot(category: "Doors"), Snapshot(category: "Windows"));

            Assert.Equal("Doors", diff.CategoryChange.OldValue);
            Assert.Equal("Windows", diff.CategoryChange.NewValue);
            Assert.True(diff.HasChanges);
        }

        [Fact]
        public void AddedParameterContainsNewState()
        {
            var diff = _engine.Compare(
                Snapshot(parameters: new[] { Width() }),
                Snapshot(parameters: new[] { Width(), Height() }));

            var change = Assert.Single(diff.ParameterChanges);
            Assert.Equal(ChangeKind.Added, change.Kind);
            Assert.Equal("family:height", change.StableKey);
            Assert.Equal("Height", change.Name);
            Assert.Null(change.Before);
            Assert.Equal(Height(), change.After);
            Assert.True(diff.HasChanges);
        }

        [Fact]
        public void RemovedParameterContainsOldState()
        {
            var diff = _engine.Compare(
                Snapshot(parameters: new[] { Width(), Height() }),
                Snapshot(parameters: new[] { Width() }));

            var change = Assert.Single(diff.ParameterChanges);
            Assert.Equal(ChangeKind.Removed, change.Kind);
            Assert.Equal(Height(), change.Before);
            Assert.Null(change.After);
        }

        [Fact]
        public void FormulaModificationIsReportedAtFieldLevelOnly()
        {
            var before = Parameter("family:offset", "Offset", ParameterDataType.Length, ParameterScope.Type, "Width / 2");
            var after = Parameter("family:offset", "Offset", ParameterDataType.Length, ParameterScope.Type, "Width * 0.45");

            var change = Assert.Single(_engine.Compare(
                Snapshot(parameters: new[] { before }),
                Snapshot(parameters: new[] { after })).ParameterChanges);

            Assert.Equal(ChangeKind.Modified, change.Kind);
            Assert.Equal("Width / 2", change.FormulaChange.OldValue);
            Assert.Equal("Width * 0.45", change.FormulaChange.NewValue);
            Assert.Null(change.NameChange);
            Assert.Null(change.DataTypeChange);
            Assert.Null(change.ScopeChange);
        }

        [Fact]
        public void FormulaWhitespaceAlreadyNormalizedBySnapshotProducesNoDiff()
        {
            var before = Parameter("family:offset", "Offset", ParameterDataType.Length, ParameterScope.Type, "Width / 2");
            var after = Parameter("family:offset", "Offset", ParameterDataType.Length, ParameterScope.Type, " Width / 2 ");

            var diff = _engine.Compare(
                Snapshot(parameters: new[] { before }),
                Snapshot(parameters: new[] { after }));

            Assert.False(diff.HasChanges);
            Assert.Empty(diff.ParameterChanges);
        }

        [Fact]
        public void ScopeModificationIsReportedAtFieldLevel()
        {
            var before = Parameter("family:offset", "Offset", ParameterDataType.Length, ParameterScope.Type, null);
            var after = Parameter("family:offset", "Offset", ParameterDataType.Length, ParameterScope.Instance, null);

            var change = Assert.Single(_engine.Compare(
                Snapshot(parameters: new[] { before }),
                Snapshot(parameters: new[] { after })).ParameterChanges);

            Assert.Equal(ParameterScope.Type, change.ScopeChange.OldValue);
            Assert.Equal(ParameterScope.Instance, change.ScopeChange.NewValue);
        }

        [Fact]
        public void DataTypeModificationIsReportedAtFieldLevel()
        {
            var before = Parameter("family:value", "Value", ParameterDataType.Number, ParameterScope.Type, null);
            var after = Parameter("family:value", "Value", ParameterDataType.Length, ParameterScope.Type, null);

            var change = Assert.Single(_engine.Compare(
                Snapshot(parameters: new[] { before }),
                Snapshot(parameters: new[] { after })).ParameterChanges);

            Assert.Equal(ParameterDataType.Number, change.DataTypeChange.OldValue);
            Assert.Equal(ParameterDataType.Length, change.DataTypeChange.NewValue);
        }

        [Fact]
        public void SameStableKeyWithChangedNameIsReportedAtFieldLevel()
        {
            var before = Parameter("shared:width", "Width", ParameterDataType.Length, ParameterScope.Type, null);
            var after = Parameter("shared:width", "OverallWidth", ParameterDataType.Length, ParameterScope.Type, null);

            var change = Assert.Single(_engine.Compare(
                Snapshot(parameters: new[] { before }),
                Snapshot(parameters: new[] { after })).ParameterChanges);

            Assert.Equal(ChangeKind.Modified, change.Kind);
            Assert.Equal("Width", change.NameChange.OldValue);
            Assert.Equal("OverallWidth", change.NameChange.NewValue);
        }

        [Fact]
        public void MultipleModifiedFieldsProduceOneParameterChange()
        {
            var before = Parameter("family:offset", "Offset", ParameterDataType.Number, ParameterScope.Type, "Width / 2");
            var after = Parameter("family:offset", "Offset", ParameterDataType.Length, ParameterScope.Instance, "Width * 0.45");

            var change = Assert.Single(_engine.Compare(
                Snapshot(parameters: new[] { before }),
                Snapshot(parameters: new[] { after })).ParameterChanges);

            Assert.Equal(ChangeKind.Modified, change.Kind);
            Assert.NotNull(change.DataTypeChange);
            Assert.NotNull(change.ScopeChange);
            Assert.NotNull(change.FormulaChange);
        }

        [Fact]
        public void DifferentStableKeysTreatRenameAsRemovalAndAddition()
        {
            var before = Parameter("family:width", "Width", ParameterDataType.Length, ParameterScope.Type, null);
            var after = Parameter("family:overall-width", "OverallWidth", ParameterDataType.Length, ParameterScope.Type, null);

            var changes = _engine.Compare(
                Snapshot(parameters: new[] { before }),
                Snapshot(parameters: new[] { after })).ParameterChanges;

            Assert.Equal(2, changes.Count);
            Assert.Equal("OverallWidth", changes[0].Name);
            Assert.Equal(ChangeKind.Added, changes[0].Kind);
            Assert.Equal("Width", changes[1].Name);
            Assert.Equal(ChangeKind.Removed, changes[1].Kind);
        }

        [Fact]
        public void ParameterChangesUseDeterministicNameThenKeyOrdering()
        {
            var diff = _engine.Compare(
                Snapshot(),
                Snapshot(parameters: new[] { Width(), Depth(), Height() }));

            Assert.Equal(new[] { "Depth", "Height", "Width" }, diff.ParameterChanges.Select(x => x.Name));
        }

        [Fact]
        public void ParameterAndMetadataDiffsAreDirectionalAndSymmetric()
        {
            var first = Snapshot(familyName: "Door", parameters: new[] { Width() });
            var second = Snapshot(familyName: "Door_Main", parameters: new[] { Width(), Height() });

            var forward = _engine.Compare(first, second);
            var reverse = _engine.Compare(second, first);

            Assert.Equal(ChangeKind.Added, Assert.Single(forward.ParameterChanges).Kind);
            Assert.Equal(ChangeKind.Removed, Assert.Single(reverse.ParameterChanges).Kind);
            Assert.Equal(forward.FamilyNameChange.OldValue, reverse.FamilyNameChange.NewValue);
            Assert.Equal(forward.FamilyNameChange.NewValue, reverse.FamilyNameChange.OldValue);
        }

        [Fact]
        public void NullSnapshotsAreRejectedPredictably()
        {
            var snapshot = Snapshot();

            var beforeError = Assert.Throws<ArgumentNullException>(() => _engine.Compare(null, snapshot));
            var afterError = Assert.Throws<ArgumentNullException>(() => _engine.Compare(snapshot, null));

            Assert.Equal("before", beforeError.ParamName);
            Assert.Equal("after", afterError.ParamName);
        }

        private static FamilySnapshot Snapshot(
            string familyName = "Door",
            string category = "Doors",
            IEnumerable<FamilyParameterSnapshot> parameters = null,
            string fingerprint = null)
        {
            return new FamilySnapshot(
                familyName,
                category,
                parameters ?? new FamilyParameterSnapshot[0],
                new FamilyTypeSnapshot[0],
                fingerprint);
        }

        private static FamilyParameterSnapshot Width()
        {
            return Parameter("family:width", "Width", ParameterDataType.Length, ParameterScope.Type, null);
        }

        private static FamilyParameterSnapshot Height()
        {
            return Parameter("family:height", "Height", ParameterDataType.Length, ParameterScope.Type, null);
        }

        private static FamilyParameterSnapshot Depth()
        {
            return Parameter("family:depth", "Depth", ParameterDataType.Length, ParameterScope.Type, null);
        }

        private static FamilyParameterSnapshot Parameter(
            string stableKey,
            string name,
            ParameterDataType dataType,
            ParameterScope scope,
            string formula)
        {
            return new FamilyParameterSnapshot(stableKey, name, dataType, scope, formula);
        }
    }
}
