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

namespace RevitGit.Revit2021.History
{
    internal sealed class RefreshHistoryExternalEventHandler : IExternalEventHandler
    {
        private readonly HistoryViewModel _viewModel;

        public RefreshHistoryExternalEventHandler(HistoryViewModel viewModel)
        {
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        }

        public void Execute(UIApplication application)
        {
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

        private static bool IsSavedFamilyPath(string path)
        {
            return !string.IsNullOrWhiteSpace(path)
                   && Path.IsPathRooted(path)
                   && string.Equals(Path.GetExtension(path), ".rfa", StringComparison.OrdinalIgnoreCase)
                   && File.Exists(path);
        }
    }
}
