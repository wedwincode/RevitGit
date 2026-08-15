using System;
using System.Globalization;
using RevitGit.Domain.Snapshots;

namespace RevitGit.UI.Compare
{
    public sealed class ParameterValueFormatter
    {
        private static readonly CultureInfo RussianCulture = CultureInfo.GetCultureInfo("ru-RU");

        public string Format(ParameterValue value, ParameterDataType dataType)
        {
            if (value == null || value.Kind == ParameterValueKind.Null) return "не задано";
            switch (value.Kind)
            {
                case ParameterValueKind.String:
                    return value.StringValue.Length == 0 ? "(пусто)" : value.StringValue;
                case ParameterValueKind.Integer:
                    return value.IntegerValue.Value.ToString("0", RussianCulture);
                case ParameterValueKind.Double:
                    return FormatNumber(value.DoubleValue.Value) + Unit(dataType);
                case ParameterValueKind.Boolean:
                    return value.BooleanValue.Value ? "Да" : "Нет";
                default:
                    throw new ArgumentOutOfRangeException(nameof(value));
            }
        }

        private static string FormatNumber(double value) => value.ToString("0.######", RussianCulture);

        private static string Unit(ParameterDataType dataType)
        {
            switch (dataType)
            {
                case ParameterDataType.Length: return " мм";
                case ParameterDataType.Area: return " мм²";
                case ParameterDataType.Volume: return " мм³";
                case ParameterDataType.Angle: return "°";
                default: return string.Empty;
            }
        }
    }
}
