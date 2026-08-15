using RevitGit.Domain.Snapshots;
using RevitGit.Revit2021.Snapshots;
using Xunit;

namespace RevitGit.Revit2021.Tests
{
    public sealed class RevitUnitNormalizerTests
    {
        [Theory]
        [InlineData(ParameterDataType.Number, "unitless")]
        [InlineData(ParameterDataType.Length, "millimeter")]
        [InlineData(ParameterDataType.Area, "square millimeter")]
        [InlineData(ParameterDataType.Volume, "cubic millimeter")]
        [InlineData(ParameterDataType.Angle, "degree")]
        public void CanonicalUnitPolicyMatchesSnapshotSchema(ParameterDataType dataType, string expected)
        {
            Assert.Equal(expected, RevitUnitNormalizer.CanonicalUnit(dataType));
        }

        [Theory]
        [InlineData(304.80000001d, 304.8d)]
        [InlineData(92903.04000001d, 92903.04d)]
        [InlineData(28316846.59200001d, 28316846.592d)]
        [InlineData(179.99999999d, 180d)]
        public void ConvertedValuesUseSchemaPrecision(double canonicalValue, double expected)
        {
            Assert.Equal(expected, RevitUnitNormalizer.NormalizeCanonical(canonicalValue));
        }

        [Fact]
        public void UnsupportedDoubleTypeHasNoCanonicalUnit()
        {
            Assert.Null(RevitUnitNormalizer.CanonicalUnit(ParameterDataType.Unknown));
        }
    }
}
