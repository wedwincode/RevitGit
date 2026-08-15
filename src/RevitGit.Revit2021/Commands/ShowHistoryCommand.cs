using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitGit.Revit2021.History;

namespace RevitGit.Revit2021.Commands
{
    [Transaction(TransactionMode.Manual)]
    public sealed class ShowHistoryCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            HistoryPaneSession.Show(commandData.Application);
            return Result.Succeeded;
        }
    }
}
