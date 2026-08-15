using Autodesk.Revit.DB;

namespace RevitGit.Revit2021.Snapshots
{
    internal static class RevitParameterStableKeyBuilder
    {
        public static string Build(FamilyParameter parameter)
        {
            if (parameter.IsShared && parameter.GUID != System.Guid.Empty)
            {
                return "shared:" + parameter.GUID.ToString("N");
            }

            var internalDefinition = parameter.Definition as InternalDefinition;
            if (internalDefinition != null
                && internalDefinition.BuiltInParameter != BuiltInParameter.INVALID)
            {
                return "builtin:" + internalDefinition.BuiltInParameter;
            }

            return "family:" + parameter.Definition.Name;
        }
    }
}
