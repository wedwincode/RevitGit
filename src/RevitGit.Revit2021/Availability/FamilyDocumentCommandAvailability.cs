using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace RevitGit.Revit2021.Availability
{
    public sealed class FamilyDocumentCommandAvailability : IExternalCommandAvailability
    {
        public bool IsCommandAvailable(UIApplication applicationData, CategorySet selectedCategories)
        {
            var activeDocument = applicationData?.ActiveUIDocument;
            return activeDocument != null
                   && activeDocument.Document != null
                   && activeDocument.Document.IsFamilyDocument;
        }
    }
}
