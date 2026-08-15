using Autodesk.Revit.DB;
using RevitGit.Domain.Snapshots;

namespace RevitGit.Revit2021.Snapshots
{
    internal static class RevitParameterDataTypeMapper
    {
        public static ParameterDataType Map(ParameterType parameterType)
        {
            return MapLegacyName(parameterType.ToString());
        }

        public static ParameterDataType MapLegacyName(string parameterTypeName)
        {
            switch (parameterTypeName)
            {
                case "Text":
                case "MultilineText":
                case "URL":
                    return ParameterDataType.Text;
                case "Integer":
                    return ParameterDataType.Integer;
                case "Number":
                    return ParameterDataType.Number;
                case "Length":
                    return ParameterDataType.Length;
                case "Area":
                    return ParameterDataType.Area;
                case "Volume":
                    return ParameterDataType.Volume;
                case "Angle":
                    return ParameterDataType.Angle;
                case "YesNo":
                    return ParameterDataType.YesNo;
                case "Material":
                    return ParameterDataType.Material;
                case "FamilyType":
                    return ParameterDataType.FamilyType;
                default:
                    return ParameterDataType.Unknown;
            }
        }
    }
}
