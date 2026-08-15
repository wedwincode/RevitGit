using RevitGit.Domain.Snapshots;
using RevitGit.Revit2021.Snapshots;
using Xunit;

namespace RevitGit.Revit2021.Tests
{
    public sealed class RevitParameterDataTypeMapperTests
    {
        [Theory]
        [InlineData("Text", ParameterDataType.Text)]
        [InlineData("MultilineText", ParameterDataType.Text)]
        [InlineData("URL", ParameterDataType.Text)]
        [InlineData("Integer", ParameterDataType.Integer)]
        [InlineData("Number", ParameterDataType.Number)]
        [InlineData("Length", ParameterDataType.Length)]
        [InlineData("Area", ParameterDataType.Area)]
        [InlineData("Volume", ParameterDataType.Volume)]
        [InlineData("Angle", ParameterDataType.Angle)]
        [InlineData("YesNo", ParameterDataType.YesNo)]
        [InlineData("Material", ParameterDataType.Material)]
        [InlineData("FamilyType", ParameterDataType.FamilyType)]
        public void SupportedLegacyTypesMapToNeutralTypes(string source, ParameterDataType expected)
        {
            Assert.Equal(expected, RevitParameterDataTypeMapper.MapLegacyName(source));
        }

        [Fact]
        public void UnsupportedLegacyTypeMapsToUnknown()
        {
            Assert.Equal(ParameterDataType.Unknown, RevitParameterDataTypeMapper.MapLegacyName("Force"));
        }
    }
}
