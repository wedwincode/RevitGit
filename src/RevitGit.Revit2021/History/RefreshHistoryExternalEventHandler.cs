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
using RevitGit.Revit2021.Restore;

namespace RevitGit.Revit2021.History
{
    internal sealed class RefreshHistoryExternalEventHandler : IExternalEventHandler
    {
        private readonly HistoryViewModel _viewModel;
        private VersionId _pendingCurrentComparison;
        private VersionId _pendingRestore;
        private PendingRevitRestore _pendingReopen;
        private bool _continueRestore;
        private string _displayedFamilyPath;
        private readonly Action _restoreCompleted;

        public RefreshHistoryExternalEventHandler(HistoryViewModel viewModel, Action restoreCompleted = null)
        {
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            _restoreCompleted = restoreCompleted;
        }

        public void Execute(UIApplication application)
        {
            if (_continueRestore)
            {
                _continueRestore = false;
                CompleteRestore(application);
                return;
            }
            var comparison = _pendingCurrentComparison;
            _pendingCurrentComparison = null;
            if (comparison != null)
            {
                CompareCurrent(application, comparison);
                return;
            }
            var restore = _pendingRestore;
            _pendingRestore = null;
            if (restore != null)
            {
                Restore(application, restore);
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

        public void QueueRestore(VersionId sourceVersionId)
        {
            if (_pendingRestore == null) _pendingRestore = sourceVersionId;
        }

        public bool QueueRestoreContinuation()
        {
            if (_pendingReopen == null || _continueRestore) return false;
            _continueRestore = true;
            return true;
        }

        private void Restore(UIApplication application, VersionId sourceVersionId)
        {
            var prepared = default(RevitGit.Application.Models.PreparedRestoreContent);
            var awaitingReopen = false;
            try
            {
                var document = application?.ActiveUIDocument?.Document;
                if (document == null || !document.IsFamilyDocument || !IsSavedFamilyPath(document.PathName)
                    || string.IsNullOrWhiteSpace(_displayedFamilyPath)
                    || !string.Equals(Path.GetFullPath(document.PathName), Path.GetFullPath(_displayedFamilyPath), StringComparison.OrdinalIgnoreCase))
                {
                    _viewModel.ShowRestoreError("Активное семейство изменилось. Повторите операцию.");
                    return;
                }
                var source = _viewModel.Versions.SingleOrDefault(item => item.Id.Equals(sourceVersionId));
                if (source == null || source.IsCurrent)
                {
                    _viewModel.ShowRestoreError(source == null
                        ? "Выбранная версия больше недоступна."
                        : "Эта версия уже текущая.");
                    return;
                }

                var dialog = new TaskDialog("История семейств")
                {
                    MainInstruction = "Восстановить версию?",
                    MainContent = "Текущее состояние семейства будет заменено содержимым выбранной версии.\n\n"
                                  + "История не будет удалена — после восстановления будет создана новая версия.\n\n"
                                  + (document.IsModified
                                      ? "В текущем семействе есть несохранённые изменения. При восстановлении они будут потеряны.\n\n"
                                      : string.Empty)
                                  + "Выбранная версия: " + source.CreatedAtText
                                  + (string.IsNullOrWhiteSpace(source.Comment) ? string.Empty : "\n\"" + source.Comment + "\"")
                };
                dialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink1, "Восстановить");
                dialog.CommonButtons = TaskDialogCommonButtons.Cancel;
                dialog.DefaultButton = TaskDialogResult.Cancel;
                if (dialog.Show() != TaskDialogResult.CommandLink1)
                {
                    _viewModel.ShowRestoreCancelled();
                    return;
                }

                var useCase = RevitCompositionRoot.CreateRestoreVersionUseCase(document);
                prepared = useCase.Prepare(sourceVersionId);
                var coordinatorInput = prepared;
                prepared = null; // coordinator owns cleanup once execution starts, including failure paths
                _pendingReopen = new RevitRestoreCoordinator().Begin(application, document, useCase, coordinatorInput);
                awaitingReopen = true;
            }
            catch (RestoreReopenException exception)
            {
                Debug.WriteLine("Family History restored file reopen failed: " + exception);
                _viewModel.ShowRestoreError("Семейство восстановлено на диске, но Revit не смог открыть его автоматически.\nОткройте файл снова:\n" + exception.FamilyPath);
            }
            catch (RepositoryCorruptedException exception)
            {
                Debug.WriteLine("Family History restore preflight failed: " + exception);
                _viewModel.ShowRestoreError("Не удалось восстановить версию: история повреждена или неполна.");
            }
            catch (GitRepositoryCorruptedException exception)
            {
                Debug.WriteLine("Family History restore topology validation failed: " + exception);
                _viewModel.ShowRestoreError("Не удалось восстановить версию: история повреждена или неполна.");
            }
            catch (Exception exception)
            {
                Debug.WriteLine("Family History restore failed: " + exception);
                _viewModel.ShowRestoreError("Не удалось восстановить версию.");
            }
            finally
            {
                if (prepared != null)
                {
                    try
                    {
                        var document = application?.ActiveUIDocument?.Document;
                        if (document != null)
                            RevitCompositionRoot.CreateRestoreVersionUseCase(document).Cleanup(prepared);
                    }
                    catch (Exception cleanupException) { Debug.WriteLine("Family History restore cleanup failed: " + cleanupException); }
                }
                if (!awaitingReopen) _restoreCompleted?.Invoke();
            }
        }

        private void CompleteRestore(UIApplication application)
        {
            var pending = _pendingReopen;
            _pendingReopen = null;
            try
            {
                if (pending == null) throw new InvalidOperationException("No restore is awaiting reopen.");
                new RevitRestoreCoordinator().Complete(application, pending);
                var reopened = application.ActiveUIDocument?.Document;
                if (reopened == null) throw new InvalidOperationException("The restored family is not active.");
                var summary = RevitCompositionRoot.CreateGetHistoryUseCase(reopened).Execute();
                var currentVariant = summary.Variants.Single(variant => variant.IsCurrent);
                _viewModel.ShowHistory(Path.GetFileName(reopened.PathName), currentVariant.Name, summary.Versions);
                _displayedFamilyPath = reopened.PathName;
                TaskDialog.Show("История семейств", "Версия восстановлена.");
            }
            catch (RestoreReopenException exception)
            {
                Debug.WriteLine("Family History restored file reopen failed: " + exception);
                _viewModel.ShowRestoreError("Семейство восстановлено на диске, но Revit не смог открыть его автоматически.\nОткройте файл снова:\n" + exception.FamilyPath);
            }
            catch (Exception exception)
            {
                Debug.WriteLine("Family History restore completion failed: " + exception);
                _viewModel.ShowRestoreError("Версия восстановлена, но не удалось обновить окно истории.");
            }
            finally
            {
                _restoreCompleted?.Invoke();
            }
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
