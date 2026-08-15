using System;

namespace RevitGit.Revit2021.Snapshots
{
    public class SnapshotExtractionException : Exception
    {
        public SnapshotExtractionException(string message)
            : base(message)
        {
        }

        public SnapshotExtractionException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }

    public sealed class NotFamilyDocumentException : SnapshotExtractionException
    {
        public NotFamilyDocumentException()
            : base("The supplied Revit document is not a family document.")
        {
        }
    }
}
