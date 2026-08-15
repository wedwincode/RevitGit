using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace RevitGit.Revit2021
{
    [Transaction(TransactionMode.ReadOnly)]
    public sealed class DiagnosticCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            TaskDialog.Show(
                "История семейств",
                "RevitGit add-in загружен.\n\nRevit integration: OK");

            return Result.Succeeded;
        }
    }
}
