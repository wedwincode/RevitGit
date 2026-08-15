using System;

namespace RevitGit.Revit2021.Restore
{
    internal sealed class RestoreReopenException : Exception
    {
        public RestoreReopenException(string familyPath, Exception innerException)
            : base("The restored family could not be reopened.", innerException)
        {
            FamilyPath = familyPath;
        }

        public string FamilyPath { get; }
    }
}
