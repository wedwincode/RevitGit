using System;
using System.Collections.Generic;
using System.Linq;
using RevitGit.Application.Models;
using RevitGit.Domain.Identifiers;
using RevitGit.UI.History;
using RevitGit.UI.Compare;
using RevitGit.Application.Diff;
using RevitGit.Domain.Snapshots;
using Xunit;

namespace RevitGit.UI.Tests.History
{
    public sealed class HistoryViewModelTests
    {
        private static readonly DateTimeOffset FirstTime = new DateTimeOffset(2026, 8, 14, 10, 15, 0, TimeSpan.FromHours(3));

        [Fact]
        public void InitialRefreshUsesLoadingStateAndRequestsRevitContext()
        {
            var refresh = new RefreshRequest();
            var viewModel = new HistoryViewModel(refresh);

            viewModel.RefreshCommand.Execute(null);

            Assert.Equal(HistoryPaneState.Loading, viewModel.State);
            Assert.Equal(1, refresh.Count);
        }

        [Theory]
        [InlineData(HistoryPaneState.NoDocument, "Откройте семейство Revit, чтобы посмотреть историю.")]
        [InlineData(HistoryPaneState.NotFamily, "История доступна только для семейств Revit (.rfa).")]
        [InlineData(HistoryPaneState.UnsavedFamily, "Сначала сохраните семейство как .rfa.")]
        public void ContextStatesHaveActionableRussianMessages(HistoryPaneState state, string expected)
        {
            var viewModel = CreateViewModel();

            viewModel.ShowContextState(state);

            Assert.Equal(state, viewModel.State);
            Assert.Equal(expected, viewModel.StatusMessage);
            Assert.Empty(viewModel.Versions);
            Assert.Null(viewModel.SelectedVersion);
        }

        [Fact]
        public void NoHistoryShowsFamilyNameAndEmptyExplanation()
        {
            var viewModel = CreateViewModel();
            viewModel.ShowNoHistory("Door.rfa");

            Assert.Equal(HistoryPaneState.NoHistory, viewModel.State);
            Assert.Equal("Door.rfa", viewModel.FamilyFileName);
            Assert.Equal("Для этого семейства ещё нет сохранённых версий.", viewModel.StatusMessage);
        }

        [Fact]
        public void PopulatedHistoryPreservesApplicationOrderAndSelectsCurrentVersion()
        {
            var first = Summary(FirstTime, "Начальная версия");
            var current = Summary(FirstTime.AddHours(2), "Изменена ширина", true, parent: first.Id);
            var viewModel = CreateViewModel();

            viewModel.ShowHistory("Door.rfa", "Основной", new[] { current, first });

            Assert.Equal(HistoryPaneState.Ready, viewModel.State);
            Assert.Equal("Основной", viewModel.CurrentVariantName);
            Assert.Equal(new[] { current.Id, first.Id }, viewModel.Versions.Select(item => item.Id));
            Assert.Equal(current.Id, viewModel.SelectedVersion.Id);
            Assert.True(viewModel.Versions[0].IsCurrent);
            Assert.Equal("Текущая", viewModel.Versions[0].CurrentLabel);
        }

        [Fact]
        public void EmptyCommentAddsNoPlaceholder()
        {
            var viewModel = CreateViewModel();
            viewModel.ShowHistory("Door.rfa", "Основной", new[] { Summary(FirstTime, null, true) });

            Assert.Null(viewModel.Versions.Single().Comment);
        }

        [Fact]
        public void RestoredVersionShowsStructuredSourceDate()
        {
            var restoredFrom = VersionId.New();
            var restored = Summary(FirstTime.AddHours(2), null, true, restoredFrom: restoredFrom, restoredFromCreatedAt: FirstTime);
            var viewModel = CreateViewModel();

            viewModel.ShowHistory("Door.rfa", "Основной", new[] { restored });

            Assert.True(viewModel.SelectedVersion.IsRestored);
            Assert.Equal("Восстановленная версия\nИсточник: 14.08.2026 10:15", viewModel.SelectedVersion.RestoredFromText);
        }

        [Fact]
        public void SelectionChangesOnlySelectedPresentationItem()
        {
            var first = Summary(FirstTime, "A");
            var current = Summary(FirstTime.AddHours(1), "B", true, parent: first.Id);
            var viewModel = CreateViewModel();
            viewModel.ShowHistory("Door.rfa", "Основной", new[] { current, first });

            viewModel.SelectedVersion = viewModel.Versions[1];

            Assert.Equal(first.Id, viewModel.SelectedVersion.Id);
        }

        [Fact]
        public void RefreshReplacesOldStateAndSelectsNewCurrentVersion()
        {
            var viewModel = CreateViewModel();
            viewModel.ShowHistory("Door.rfa", "Основной", new[] { Summary(FirstTime, "A", true) });
            var next = Summary(FirstTime.AddHours(1), "B", true);

            viewModel.ShowHistory("Window.rfa", "Основной", new[] { next });

            Assert.Equal("Window.rfa", viewModel.FamilyFileName);
            Assert.Single(viewModel.Versions);
            Assert.Equal(next.Id, viewModel.SelectedVersion.Id);
        }

        [Fact]
        public void ErrorClearsStaleHistoryWithoutExposingTechnicalDetails()
        {
            var viewModel = CreateViewModel();
            viewModel.ShowHistory("Door.rfa", "Основной", new[] { Summary(FirstTime, "A", true) });

            viewModel.ShowError(true);

            Assert.Equal(HistoryPaneState.Error, viewModel.State);
            Assert.Equal("История повреждена или неполна.", viewModel.StatusMessage);
            Assert.Empty(viewModel.Versions);
            Assert.Null(viewModel.SelectedVersion);
        }

        [Fact]
        public void OneHundredVersionsRemainAvailableForVirtualizedView()
        {
            var versions = Enumerable.Range(0, 100)
                .Select(index => Summary(FirstTime.AddMinutes(index), "Version " + index, index == 99))
                .Reverse()
                .ToArray();
            var viewModel = CreateViewModel();

            viewModel.ShowHistory("Door.rfa", "Основной", versions);

            Assert.Equal(100, viewModel.Versions.Count);
            Assert.Equal(versions[0].Id, viewModel.SelectedVersion.Id);
        }

        [Fact]
        public void LongCommentIsPreservedForSelectedDetails()
        {
            var comment = new string('А', 500);
            var viewModel = CreateViewModel();

            viewModel.ShowHistory("Door.rfa", "Основной", new[] { Summary(FirstTime, comment, true) });

            Assert.Equal(comment, viewModel.SelectedVersion.Comment);
        }

        [Fact]
        public void CompareWithCurrentSetsBusyPreventsOverlapAndBackPreservesSelection()
        {
            var request = new CompareRequest();
            var viewModel = new HistoryViewModel(new RefreshRequest(), request);
            var source = Summary(FirstTime, "A", true);
            viewModel.ShowHistory("Door.rfa", "Основной", new[] { source });

            viewModel.CompareWithCurrentCommand.Execute(null);
            Assert.True(viewModel.IsBusy);
            Assert.False(viewModel.CompareWithCurrentCommand.CanExecute(null));
            Assert.Equal(source.Id, request.CurrentSource);

            var snapshot = new FamilySnapshot("Door", "Doors", new FamilyParameterSnapshot[0], new FamilyTypeSnapshot[0], "a");
            viewModel.ShowComparison(new CompareViewModel(new FamilyDiffEngine().Compare(snapshot, snapshot),
                new ComparisonSideViewModel("A", null), new ComparisonSideViewModel("Текущее состояние", null)));
            Assert.True(viewModel.IsComparePage);
            viewModel.BackToHistoryCommand.Execute(null);
            Assert.True(viewModel.IsHistoryPage);
            Assert.Equal(source.Id, viewModel.SelectedVersion.Id);
        }

        [Fact]
        public void SavedComparisonUsesSelectedSourceAndExplicitTargetDirection()
        {
            var request = new CompareRequest();
            var viewModel = new HistoryViewModel(new RefreshRequest(), request);
            var target = Summary(FirstTime, "A");
            var source = Summary(FirstTime.AddHours(1), "B", true);
            viewModel.ShowHistory("Door.rfa", "Основной", new[] { source, target });

            viewModel.ChooseOtherVersionCommand.Execute(null);
            viewModel.SelectedComparisonTarget = viewModel.Versions.Single(item => item.Id.Equals(target.Id));
            viewModel.ConfirmSavedComparisonCommand.Execute(null);

            Assert.Equal(source.Id, request.SavedSource);
            Assert.Equal(target.Id, request.SavedTarget);
        }

        [Fact]
        public void CompareFailureShowsRecoverablePageAndBackReturnsToSameSelection()
        {
            var viewModel = new HistoryViewModel(new RefreshRequest(), new CompareRequest());
            var source = Summary(FirstTime, "A", true);
            viewModel.ShowHistory("Door.rfa", "Основной", new[] { source });
            viewModel.CompareWithCurrentCommand.Execute(null);

            viewModel.ShowCompareError("Не удалось получить текущее состояние семейства.");

            Assert.True(viewModel.IsComparePage);
            Assert.False(viewModel.IsBusy);
            Assert.Equal("Не удалось получить текущее состояние семейства.", viewModel.CompareErrorMessage);
            viewModel.BackToHistoryCommand.Execute(null);
            Assert.Equal(source.Id, viewModel.SelectedVersion.Id);
        }

        [Fact]
        public void RestoreIsEnabledOnlyForHistoricalVersionAndBusyPreventsOverlap()
        {
            var request = new RestoreRequest();
            var historical = Summary(FirstTime, "A");
            var current = Summary(FirstTime.AddHours(1), "B", true, parent: historical.Id);
            var viewModel = new HistoryViewModel(new RefreshRequest(), null, request);
            viewModel.ShowHistory("Door.rfa", "Основной", new[] { current, historical });

            Assert.False(viewModel.RestoreCommand.CanExecute(null));
            viewModel.SelectedVersion = viewModel.Versions[1];
            Assert.True(viewModel.RestoreCommand.CanExecute(null));
            viewModel.RestoreCommand.Execute(null);

            Assert.Equal(historical.Id, request.Source);
            Assert.True(viewModel.IsBusy);
            Assert.Equal("Восстановление…", viewModel.RestoreStatusMessage);
            Assert.False(viewModel.RestoreCommand.CanExecute(null));
        }

        [Fact]
        public void RestoreCancelAndFailureKeepHistoryAvailable()
        {
            var request = new RestoreRequest();
            var historical = Summary(FirstTime, "A");
            var current = Summary(FirstTime.AddHours(1), "B", true);
            var viewModel = new HistoryViewModel(new RefreshRequest(), null, request);
            viewModel.ShowHistory("Door.rfa", "Основной", new[] { current, historical });
            viewModel.SelectedVersion = viewModel.Versions[1];
            viewModel.RestoreCommand.Execute(null);

            viewModel.ShowRestoreCancelled();
            Assert.False(viewModel.IsBusy);
            Assert.Equal(historical.Id, viewModel.SelectedVersion.Id);
            viewModel.RestoreCommand.Execute(null);
            viewModel.ShowRestoreError("Не удалось восстановить версию.");
            Assert.Equal(HistoryPaneState.Ready, viewModel.State);
            Assert.Equal("Не удалось восстановить версию.", viewModel.RestoreStatusMessage);
        }

        private static HistoryViewModel CreateViewModel() => new HistoryViewModel(new RefreshRequest());

        private static VersionSummary Summary(
            DateTimeOffset createdAt,
            string comment,
            bool current = false,
            VersionId parent = null,
            VersionId restoredFrom = null,
            DateTimeOffset? restoredFromCreatedAt = null)
        {
            return new VersionSummary(VersionId.New(), parent, createdAt, comment, restoredFrom, current, restoredFromCreatedAt);
        }

        private sealed class RefreshRequest : IHistoryRefreshRequest
        {
            public int Count { get; private set; }
            public void RequestRefresh() { Count++; }
        }

        private sealed class CompareRequest : IHistoryCompareRequest
        {
            public VersionId CurrentSource { get; private set; }
            public VersionId SavedSource { get; private set; }
            public VersionId SavedTarget { get; private set; }
            public void RequestCompareWithCurrent(VersionId sourceVersionId) { CurrentSource = sourceVersionId; }
            public void RequestCompareSaved(VersionId sourceVersionId, VersionId targetVersionId)
            { SavedSource = sourceVersionId; SavedTarget = targetVersionId; }
        }

        private sealed class RestoreRequest : IHistoryRestoreRequest
        {
            public VersionId Source { get; private set; }
            public void RequestRestore(VersionId sourceVersionId) { Source = sourceVersionId; }
        }
    }
}
