namespace RevitGit.Revit2021.Snapshots
{
    internal static class RevitFamilyTypeNameResolver
    {
        public static string Resolve(string typeName, string familyName)
        {
            return string.IsNullOrWhiteSpace(typeName)
                ? familyName.Trim()
                : typeName.Trim();
        }
    }
}
