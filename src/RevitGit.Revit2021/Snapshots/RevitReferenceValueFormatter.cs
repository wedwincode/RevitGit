using RevitGit.Domain.Snapshots;

namespace RevitGit.Revit2021.Snapshots
{
    internal static class RevitReferenceValueFormatter
    {
        public static ParameterValue Material(string materialName)
        {
            return NamedElement(materialName);
        }

        public static ParameterValue FamilyType(string familyName, string typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName))
            {
                return ParameterValue.Null;
            }

            return string.IsNullOrWhiteSpace(familyName)
                ? ParameterValue.FromString(typeName.Trim())
                : ParameterValue.FromString(familyName.Trim() + " : " + typeName.Trim());
        }

        public static ParameterValue NamedElement(string elementName)
        {
            return string.IsNullOrWhiteSpace(elementName)
                ? ParameterValue.Null
                : ParameterValue.FromString(elementName.Trim());
        }
    }
}
