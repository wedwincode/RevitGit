using System;
using System.Diagnostics;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitGit.Revit2021.Snapshots;

namespace RevitGit.Revit2021
{
    [Transaction(TransactionMode.ReadOnly)]
    public sealed class DiagnosticCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var activeDocument = commandData.Application.ActiveUIDocument;
            if (activeDocument == null)
            {
                TaskDialog.Show(
                    "История семейств",
                    "Revit integration: OK\n\nSnapshot extraction: no active document.");
                return Result.Succeeded;
            }

            var document = activeDocument.Document;
            if (!document.IsFamilyDocument)
            {
                TaskDialog.Show(
                    "История семейств",
                    "Snapshot extraction: not applicable\n\nActive document is not a family document.");
                return Result.Succeeded;
            }

            try
            {
                var extractor = new RevitFamilySnapshotExtractor();
                var stopwatch = Stopwatch.StartNew();
                var snapshot = extractor.Extract(document);
                stopwatch.Stop();
                var repeatedSnapshot = extractor.Extract(document);
                var deterministic = snapshot.Equals(repeatedSnapshot);
                var fingerprintPrefix = snapshot.GeometryFingerprint.Substring(
                    0,
                    Math.Min(12, snapshot.GeometryFingerprint.Length));

                TaskDialog.Show(
                    "История семейств",
                    "Snapshot extraction: " + (deterministic ? "OK" : "NON-DETERMINISTIC")
                    + "\n\nFamily: " + snapshot.FamilyName
                    + "\nCategory: " + snapshot.Category
                    + "\nParameters: " + snapshot.Parameters.Count
                    + "\nTypes: " + snapshot.Types.Count
                    + "\nGeometry fingerprint: " + fingerprintPrefix + "..."
                    + "\nRepeated extraction equal: " + (deterministic ? "YES" : "NO")
                    + "\nExtraction time: " + stopwatch.ElapsedMilliseconds + " ms");

                if (!deterministic)
                {
                    message = "Repeated extraction produced different snapshots without a document change.";
                    return Result.Failed;
                }

                return Result.Succeeded;
            }
            catch (Exception exception)
            {
                Debug.WriteLine("Family snapshot extraction failed: " + exception);
                message = exception.Message;
                TaskDialog.Show(
                    "История семейств",
                    "Snapshot extraction failed.\n\n" + exception.Message);
                return Result.Failed;
            }
        }
    }
}
