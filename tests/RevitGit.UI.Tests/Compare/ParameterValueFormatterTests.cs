using RevitGit.Domain.Snapshots;
using RevitGit.UI.Compare;
using Xunit;

namespace RevitGit.UI.Tests.Compare
{
    public sealed class ParameterValueFormatterTests
    {
        private readonly ParameterValueFormatter _formatter = new ParameterValueFormatter();

        [Theory]
        [InlineData(ParameterDataType.Length, 900d, "900 мм")]
        [InlineData(ParameterDataType.Area, 12.5d, "12,5 мм²")]
        [InlineData(ParameterDataType.Volume, 0.125d, "0,125 мм³")]
        [InlineData(ParameterDataType.Angle, 45d, "45°")]
        [InlineData(ParameterDataType.Number, 12.5d, "12,5")]
        public void FormatsCanonicalDoubleValues(ParameterDataType type, double value, string expected)
        {
            Assert.Equal(expected, _formatter.Format(ParameterValue.FromDouble(value), type));
        }

        [Fact]
        public void FormatsBooleanNullStringsAndReferencesInRussian()
        {
            Assert.Equal("Да", _formatter.Format(ParameterValue.FromBoolean(true), ParameterDataType.YesNo));
            Assert.Equal("Нет", _formatter.Format(ParameterValue.FromBoolean(false), ParameterDataType.YesNo));
            Assert.Equal("не задано", _formatter.Format(ParameterValue.Null, ParameterDataType.Text));
            Assert.Equal("(пусто)", _formatter.Format(ParameterValue.FromString(""), ParameterDataType.Text));
            Assert.Equal("Стекло", _formatter.Format(ParameterValue.FromString("Стекло"), ParameterDataType.Material));
            Assert.Equal("Ручка A", _formatter.Format(ParameterValue.FromString("Ручка A"), ParameterDataType.FamilyType));
        }

        [Fact]
        public void FormatsIntegerWithoutDecimalNoise()
        {
            Assert.Equal("42", _formatter.Format(ParameterValue.FromInteger(42), ParameterDataType.Integer));
        }
    }
}
