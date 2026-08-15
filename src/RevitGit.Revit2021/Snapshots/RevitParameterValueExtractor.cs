using Autodesk.Revit.DB;
using RevitGit.Domain.Snapshots;
using ParameterValue = RevitGit.Domain.Snapshots.ParameterValue;

namespace RevitGit.Revit2021.Snapshots
{
    internal static class RevitParameterValueExtractor
    {
        public static bool TryExtract(
            Document document,
            FamilyType familyType,
            FamilyParameter parameter,
            ParameterDataType dataType,
            out ParameterValue value)
        {
            value = null;
            if (parameter.IsInstance || dataType == ParameterDataType.Unknown)
            {
                return false;
            }

            if (!familyType.HasValue(parameter))
            {
                value = ParameterValue.Null;
                return true;
            }

            switch (parameter.StorageType)
            {
                case StorageType.String:
                    var stringValue = familyType.AsString(parameter);
                    value = stringValue == null
                        ? ParameterValue.Null
                        : ParameterValue.FromString(stringValue);
                    return true;
                case StorageType.Integer:
                    value = RevitParameterValuePolicy.FromInteger(familyType.AsInteger(parameter), dataType);
                    return true;
                case StorageType.Double:
                    var doubleValue = familyType.AsDouble(parameter);
                    value = doubleValue.HasValue
                        ? ParameterValue.FromDouble(RevitUnitNormalizer.ToCanonical(doubleValue.Value, dataType))
                        : ParameterValue.Null;
                    return true;
                case StorageType.ElementId:
                    value = ExtractReference(document, familyType.AsElementId(parameter), dataType);
                    return true;
                case StorageType.None:
                    value = ParameterValue.Null;
                    return true;
                default:
                    return false;
            }
        }

        private static ParameterValue ExtractReference(
            Document document,
            ElementId elementId,
            ParameterDataType dataType)
        {
            if (elementId == null || elementId == ElementId.InvalidElementId)
            {
                return ParameterValue.Null;
            }

            var element = document.GetElement(elementId);
            if (element == null)
            {
                return ParameterValue.Null;
            }

            if (dataType == ParameterDataType.Material)
            {
                var material = element as Material;
                return RevitReferenceValueFormatter.Material(material == null ? element.Name : material.Name);
            }

            if (dataType == ParameterDataType.FamilyType)
            {
                var elementType = element as ElementType;
                return elementType == null
                    ? RevitReferenceValueFormatter.NamedElement(element.Name)
                    : RevitReferenceValueFormatter.FamilyType(elementType.FamilyName, elementType.Name);
            }

            return RevitReferenceValueFormatter.NamedElement(element.Name);
        }
    }
}
