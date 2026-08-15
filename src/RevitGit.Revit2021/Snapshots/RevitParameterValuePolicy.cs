using RevitGit.Domain.Snapshots;

namespace RevitGit.Revit2021.Snapshots
{
    internal static class RevitParameterValuePolicy
    {
        public static ParameterValue FromInteger(int? value, ParameterDataType dataType)
        {
            if (!value.HasValue)
            {
                return ParameterValue.Null;
            }

            return dataType == ParameterDataType.YesNo
                ? ParameterValue.FromBoolean(value.Value != 0)
                : ParameterValue.FromInteger(value.Value);
        }

        public static FamilyParameterSnapshot CreateParameterSnapshot(
            string stableKey,
            string name,
            ParameterDataType dataType,
            ParameterScope scope,
            string formula)
        {
            return new FamilyParameterSnapshot(stableKey, name, dataType, scope, formula);
        }
    }
}
