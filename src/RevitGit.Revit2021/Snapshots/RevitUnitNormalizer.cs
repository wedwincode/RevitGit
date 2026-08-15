using System;
using Autodesk.Revit.DB;
using RevitGit.Domain.Snapshots;

namespace RevitGit.Revit2021.Snapshots
{
    internal static class RevitUnitNormalizer
    {
        public static double ToCanonical(double internalValue, ParameterDataType dataType)
        {
            double converted;
            switch (dataType)
            {
                case ParameterDataType.Number:
                    converted = internalValue;
                    break;
                case ParameterDataType.Length:
                    converted = ToMillimeters(internalValue);
                    break;
                case ParameterDataType.Area:
                    converted = ToSquareMillimeters(internalValue);
                    break;
                case ParameterDataType.Volume:
                    converted = ToCubicMillimeters(internalValue);
                    break;
                case ParameterDataType.Angle:
                    converted = UnitUtils.ConvertFromInternalUnits(
                        internalValue,
                        UnitTypeId.Degrees);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(dataType),
                        dataType,
                        "The parameter data type has no schema 1 canonical double unit.");
            }

            return NormalizeCanonical(converted);
        }

        public static double NormalizeCanonical(double canonicalValue)
        {
            return SnapshotNumericPolicy.Normalize(canonicalValue);
        }

        public static string CanonicalUnit(ParameterDataType dataType)
        {
            switch (dataType)
            {
                case ParameterDataType.Number:
                    return SnapshotNumericPolicy.NumberUnit;
                case ParameterDataType.Length:
                    return SnapshotNumericPolicy.LengthUnit;
                case ParameterDataType.Area:
                    return SnapshotNumericPolicy.AreaUnit;
                case ParameterDataType.Volume:
                    return SnapshotNumericPolicy.VolumeUnit;
                case ParameterDataType.Angle:
                    return SnapshotNumericPolicy.AngleUnit;
                default:
                    return null;
            }
        }

        public static double ToMillimeters(double internalValue)
        {
            return NormalizeCanonical(
                UnitUtils.ConvertFromInternalUnits(internalValue, UnitTypeId.Millimeters));
        }

        public static double ToSquareMillimeters(double internalValue)
        {
            return NormalizeCanonical(
                UnitUtils.ConvertFromInternalUnits(internalValue, UnitTypeId.SquareMillimeters));
        }

        public static double ToCubicMillimeters(double internalValue)
        {
            return NormalizeCanonical(
                UnitUtils.ConvertFromInternalUnits(internalValue, UnitTypeId.CubicMillimeters));
        }
    }
}
