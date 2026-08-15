using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitGit.Revit2021.Composition;
using RevitGit.Revit2021.Diagnostics;
using RevitGit.Revit2021.Presentation;

namespace RevitGit.Revit2021.Commands
{
    [Transaction(TransactionMode.Manual)]
    public sealed class SaveVersionCommand : IExternalCommand
    {
        private const string DialogTitle = "История семейств";

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var activeDocument = commandData?.Application?.ActiveUIDocument;
            var document = activeDocument?.Document;
            if (document == null || !document.IsFamilyDocument)
            {
                TaskDialog.Show(
                    DialogTitle,
                    "Сохранение версии доступно только для открытого семейства Revit.");
                return Result.Succeeded;
            }

            if (!IsSavedFamilyPath(document.PathName))
            {
                TaskDialog.Show(
                    DialogTitle,
                    "Сначала сохраните семейство как .rfa, затем создайте первую версию.");
                return Result.Succeeded;
            }

            var prompt = new WpfSaveVersionCommentPrompt(commandData.Application.MainWindowHandle);
            var promptResult = prompt.Show();
            var workflow = new SaveVersionCommandWorkflow();
            if (!promptResult.Confirmed)
            {
                return Result.Cancelled;
            }

            var timings = new SaveVersionTimings();
            try
            {
                var operation = RevitCompositionRoot.CreateSaveVersionOperation(document, timings);
                var result = workflow.Execute(promptResult, operation);
                if (result.Cancelled)
                {
                    return Result.Cancelled;
                }

                var version = result.Version;
                var confirmation = "Версия сохранена.\n\n"
                                   + version.CreatedAt.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);
                if (version.Comment != null)
                {
                    confirmation += "\nКомментарий: " + version.Comment;
                }

                TaskDialog.Show(DialogTitle, confirmation);
                return Result.Succeeded;
            }
            catch (Exception exception)
            {
                Debug.WriteLine("Save Version failed: " + exception);
                message = SaveVersionErrorPresenter.GetUserMessage(exception);
                return Result.Failed;
            }
        }

        private static bool IsSavedFamilyPath(string path)
        {
            return !string.IsNullOrWhiteSpace(path)
                   && Path.IsPathRooted(path)
                   && string.Equals(Path.GetExtension(path), ".rfa", StringComparison.OrdinalIgnoreCase);
        }
    }
}
