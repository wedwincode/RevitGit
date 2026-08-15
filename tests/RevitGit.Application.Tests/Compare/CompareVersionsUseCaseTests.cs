using System;
using System.Collections.Generic;
using RevitGit.Application.Compare;
using RevitGit.Application.Exceptions;
using RevitGit.Application.Models;
using RevitGit.Application.Tests.Fakes;
using RevitGit.Domain.Exceptions;
using RevitGit.Domain.History;
using RevitGit.Domain.Identifiers;
using RevitGit.Domain.Snapshots;
using Xunit;

namespace RevitGit.Application.Tests.Compare
{
    public sealed class CompareVersionsUseCaseTests
    {
        private static readonly FamilyIdentity Identity = new FamilyIdentity("family-1");
        private static readonly DateTimeOffset Time = new DateTimeOffset(2026, 8, 15, 10, 0, 0, TimeSpan.Zero);

        [Fact]
        public void SavedVersionsAreLoadedOnDemandAndComparedInRequestedDirection()
        {
            var setup = CreateHistory();
            var snapshots = new FakeVersionSnapshotStore();
            snapshots.Seed(setup.First, Snapshot("Door"));
            snapshots.Seed(setup.Second, Snapshot("Door_Main"));

            var diff = new CompareSavedVersionsUseCase(setup.Repository, snapshots, new Application.Diff.FamilyDiffEngine())
                .Execute(Identity, setup.First, setup.Second);

            Assert.Equal("Door", diff.FamilyNameChange.OldValue);
            Assert.Equal("Door_Main", diff.FamilyNameChange.NewValue);
            Assert.Equal(new[] { setup.First, setup.Second }, snapshots.ReadVersionIds);
        }

        [Fact]
        public void SuppliedCurrentSnapshotIsComparedAfterSelectedSavedVersion()
        {
            var setup = CreateHistory();
            var snapshots = new FakeVersionSnapshotStore();
            snapshots.Seed(setup.First, Snapshot("Door"));

            var diff = new CompareVersionWithSnapshotUseCase(setup.Repository, snapshots, new Application.Diff.FamilyDiffEngine())
                .Execute(Identity, setup.First, Snapshot("Door_Current"));

            Assert.Equal("Door", diff.FamilyNameChange.OldValue);
            Assert.Equal("Door_Current", diff.FamilyNameChange.NewValue);
            Assert.Single(snapshots.ReadVersionIds);
        }

        [Fact]
        public void UnknownVersionFailsBeforeSnapshotRead()
        {
            var setup = CreateHistory();
            var snapshots = new FakeVersionSnapshotStore();
            var unknown = VersionId.New();

            Assert.Throws<UnknownVersionException>(() =>
                new CompareVersionWithSnapshotUseCase(setup.Repository, snapshots, new Application.Diff.FamilyDiffEngine())
                    .Execute(Identity, unknown, Snapshot("Current")));
            Assert.Empty(snapshots.ReadVersionIds);
        }

        [Fact]
        public void SnapshotReadFailureIsTranslatedAtApplicationBoundary()
        {
            var setup = CreateHistory();
            var snapshots = new FakeVersionSnapshotStore { ReadException = new InvalidOperationException("corrupt") };

            var error = Assert.Throws<ApplicationOperationException>(() =>
                new CompareVersionWithSnapshotUseCase(setup.Repository, snapshots, new Application.Diff.FamilyDiffEngine())
                    .Execute(Identity, setup.First, Snapshot("Current")));

            Assert.Equal(ApplicationFailureStage.ReadVersionSnapshot, error.Stage);
        }

        [Fact]
        public void VersionsFromDifferentVariantsCanBeCompared()
        {
            var history = FamilyHistory.Create("Main", Time, "A");
            var first = history.GetVariant(history.CurrentVariantId).CurrentVersionId;
            var alternative = history.CreateVariant(first, "Alternative");
            history.SwitchVariant(alternative.Id);
            var otherVariantVersion = history.AddVersion(Time.AddMinutes(1), "B").Id;
            var repository = new FakeHistoryRepository();
            repository.Seed(Identity, history);
            var snapshots = new FakeVersionSnapshotStore();
            snapshots.Seed(first, Snapshot("Door"));
            snapshots.Seed(otherVariantVersion, Snapshot("Window"));

            var diff = new CompareSavedVersionsUseCase(repository, snapshots, new Application.Diff.FamilyDiffEngine())
                .Execute(Identity, first, otherVariantVersion);

            Assert.Equal("Window", diff.FamilyNameChange.NewValue);
        }

        private static Setup CreateHistory()
        {
            var history = FamilyHistory.Create("Main", Time, "A");
            var first = history.GetVariant(history.CurrentVariantId).CurrentVersionId;
            var second = history.AddVersion(Time.AddMinutes(1), "B").Id;
            var repository = new FakeHistoryRepository();
            repository.Seed(Identity, history);
            return new Setup(repository, first, second);
        }

        private static FamilySnapshot Snapshot(string name) => new FamilySnapshot(
            name, "Doors", new FamilyParameterSnapshot[0], new FamilyTypeSnapshot[0], "geometry");

        private sealed class Setup
        {
            public Setup(FakeHistoryRepository repository, VersionId first, VersionId second)
            { Repository = repository; First = first; Second = second; }
            public FakeHistoryRepository Repository { get; }
            public VersionId First { get; }
            public VersionId Second { get; }
        }
    }
}
