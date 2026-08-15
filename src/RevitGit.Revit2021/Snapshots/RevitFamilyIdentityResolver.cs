using System;
using Autodesk.Revit.DB;

namespace RevitGit.Revit2021.Snapshots
{
    internal static class RevitFamilyIdentityResolver
    {
        public static RevitFamilyIdentity Resolve(Document document)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            var ownerFamily = document.OwnerFamily ?? FindOwnerFamilyElement(document);
            var familyName = ResolveFamilyName(ownerFamily?.Name, document.Title);
            var categoryName = ownerFamily?.FamilyCategory?.Name;
            if (string.IsNullOrWhiteSpace(categoryName))
            {
                throw new SnapshotExtractionException(
                    "The family category is unavailable from Document.OwnerFamily and the owner Family element.");
            }

            return new RevitFamilyIdentity(familyName, categoryName);
        }

        internal static string ResolveFamilyName(string ownerFamilyName, string documentTitle)
        {
            if (!string.IsNullOrWhiteSpace(ownerFamilyName))
            {
                return ownerFamilyName;
            }

            if (string.IsNullOrWhiteSpace(documentTitle))
            {
                throw new SnapshotExtractionException(
                    "The family name is unavailable from the owner Family element and Document.Title.");
            }

            var familyName = documentTitle.Trim();
            if (familyName.EndsWith(".rfa", StringComparison.OrdinalIgnoreCase))
            {
                familyName = familyName.Substring(0, familyName.Length - 4).TrimEnd();
            }

            if (familyName.Length == 0)
            {
                throw new SnapshotExtractionException(
                    "The family name is unavailable from the owner Family element and Document.Title.");
            }

            return familyName;
        }

        private static Family FindOwnerFamilyElement(Document document)
        {
            foreach (Family family in new FilteredElementCollector(document).OfClass(typeof(Family)))
            {
                if (family.IsOwnerFamily)
                {
                    return family;
                }
            }

            return null;
        }
    }

    internal sealed class RevitFamilyIdentity
    {
        public RevitFamilyIdentity(string familyName, string categoryName)
        {
            FamilyName = familyName;
            CategoryName = categoryName;
        }

        public string FamilyName { get; }

        public string CategoryName { get; }
    }
}
