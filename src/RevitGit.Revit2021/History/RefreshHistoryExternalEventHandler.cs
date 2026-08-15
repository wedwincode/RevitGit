using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Autodesk.Revit.UI;
using RevitGit.Application.Exceptions;
using RevitGit.Infrastructure.FileSystem.Exceptions;
using RevitGit.Infrastructure.Git;
using RevitGit.Revit2021.Composition;
using RevitGit.UI.History;
using RevitGit.UI.Compare;
using RevitGit.Domain.Identifiers;
using RevitGit.Revit2021.Snapshots;

namespace RevitGit.Revit2021.History
{
    internal sealed class RefreshHistoryExternalEventHandler : IExternalEventHandler
    {
        private readonly HistoryViewModel _viewModel;
        private VersionId _pendingCurrentComparison;
        private string _displayedFamilyPath;

        public RefreshHistoryExternalEventHandler(HistoryViewModel viewModel)
        {
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        }

        public void Execute(UIApplication application)
        {
            var comparison = _pendingCurrentComparison;
            _pendingCurrentComparison = null;
            if (comparison != null)
            {
                CompareCurrent(application, comparison);
                return;
            }
            var stopwatch = Stopwatch.StartNew();
            try
            {
                var document = application?.ActiveUIDocument?.Document;
                if (document == null)
                {
                    _viewModel.ShowContextState(HistoryPaneState.NoDocument);
                    return;
                }

                if (!document.IsFamilyDocument)
                {
                    _viewModel.ShowContextState(HistoryPaneState.NotFamily);
                    return;
                }

                if (!IsSavedFamilyPath(document.PathName))
                {
                    _viewModel.ShowContextState(HistoryPaneState.UnsavedFamily);
                    return;
                }

                var summary = RevitCompositionRoot.CreateGetHistoryUseCase(document).Execute();
                var currentVariant = summary.Variants.Single(variant => variant.IsCurrent);
                _viewModel.ShowHistory(
                    Path.GetFileName(document.PathName),
                    currentVariant.Name,
                    summary.Versions);
                _displayedFamilyPath = document.PathName;
            }
            catch (HistoryNotInitializedException)
            {
                var path = application?.ActiveUIDocument?.Document?.PathName;
                _viewModel.ShowNoHistory(string.IsNullOrWhiteSpace(path) ? null : Path.GetFileName(path));
            }
            catch (RepositoryCorruptedException exception)
            {
                Debug.WriteLine("Family History repository is corrupted: " + exception);
                _viewModel.ShowError(true);
            }
            catch (GitRepositoryCorruptedException exception)
            {
                Debug.WriteLine("Family History backing topology is corrupted: " + exception);
                _viewModel.ShowError(true);
            }
            catch (Exception exception)
            {
                Debug.WriteLine("Family History pane refresh failed: " + exception);
                _viewModel.ShowError(false);
            }
            finally
            {
                stopwatch.Stop();
                Debug.WriteLine("Family History pane refresh: " + stopwatch.ElapsedMilliseconds + " ms.");
            }
        }

        public string GetName() => "Обновление истории семейства";

        public void QueueCompareWithCurrent(VersionId sourceVersionId)
        {
            if (_pendingCurrentComparison == null) _pendingCurrentComparison = sourceVersionId;
        }

        public void CompareSaved(VersionId sourceVersionId, VersionId targetVersionId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_displayedFamilyPath))
                    throw new InvalidOperationException("No family history is displayed.");
                var diff = RevitCompositionRoot.CompareSavedVersions(_displayedFamilyPath, sourceVersionId, targetVersionId);
                var source = _viewModel.Versions.Single(item => item.Id.Equals(sourceVersionId));
                var target = _viewModel.Versions.Single(item => item.Id.Equals(targetVersionId));
                _viewModel.ShowComparison(new CompareViewModel(diff,
                    new ComparisonSideViewModel(source.CreatedAtText, source.Comment),
                    new ComparisonSideViewModel(target.CreatedAtText, target.Comment)));
            }
            catch (ApplicationOperationException exception) when (exception.Stage == ApplicationFailureStage.ReadVersionSnapshot)
            {
                Debug.WriteLine("Family History saved comparison snapshot read failed: " + exception);
                _viewModel.ShowCompareError("Не удалось прочитать данные выбранной версии.");
            }
            catch (Exception exception)
            {
                Debug.WriteLine("Family History saved comparison failed: " + exception);
                _viewModel.ShowCompareError("Не удалось сравнить выбранные версии.");
            }
        }

        private void CompareCurrent(UIApplication application, VersionId sourceVersionId)
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                var document = application?.ActiveUIDocument?.Document;
                if (document == null || !document.IsFamilyDocument || !IsSavedFamilyPath(document.PathName)
                    || string.IsNullOrWhiteSpace(_displayedFamilyPath)
                    || !string.Equals(Path.GetFullPath(document.PathName), Path.GetFullPath(_displayedFamilyPath), StringComparison.OrdinalIgnoreCase))
                {
                    _viewModel.ShowCompareError("Активное семейство изменилось. Обновите историю и повторите сравнение.");
                    return;
                }

                var extraction = Stopwatch.StartNew();
                var current = new RevitFamilySnapshotExtractor().Extract(document);
                extraction.Stop();
                var diffTimer = Stopwatch.StartNew();
                var diff = RevitCompositionRoot.CompareVersionWithSnapshot(document.PathName, sourceVersionId, current);
                diffTimer.Stop();
                var source = _viewModel.Versions.Single(item => item.Id.Equals(sourceVersionId));
                _viewModel.ShowComparison(new CompareViewModel(diff,
                    new ComparisonSideViewModel(source.CreatedAtText, source.Comment),
                    new ComparisonSideViewModel("Текущее состояние", null)));
                Debug.WriteLine("Family History compare current extraction: " + extraction.ElapsedMilliseconds + " ms; diff/read/map: " + diffTimer.ElapsedMilliseconds + " ms.");
            }
            catch (SnapshotExtractionException exception)
            {
                Debug.WriteLine("Family History current snapshot extraction failed: " + exception);
                _viewModel.ShowCompareError("Не удалось получить текущее состояние семейства.");
            }
            catch (ApplicationOperationException exception) when (exception.Stage == ApplicationFailureStage.ReadVersionSnapshot)
            {
                Debug.WriteLine("Family History current comparison snapshot read failed: " + exception);
                _viewModel.ShowCompareError("Не удалось прочитать данные выбранной версии.");
            }
            catch (Exception exception)
            {
                Debug.WriteLine("Family History current comparison failed: " + exception);
                _viewModel.ShowCompareError("Не удалось сравнить версию с текущим состоянием.");
            }
            finally
            {
                stopwatch.Stop();
                Debug.WriteLine("Family History compare current total: " + stopwatch.ElapsedMilliseconds + " ms.");
            }
        }

        private static bool IsSavedFamilyPath(string path)
        {
            return !string.IsNullOrWhiteSpace(path)
                   && Path.IsPathRooted(path)
                   && string.Equals(Path.GetExtension(path), ".rfa", StringComparison.OrdinalIgnoreCase)
                   && File.Exists(path);
        }
    }
}
