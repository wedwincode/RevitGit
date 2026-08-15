using RevitGit.Domain.Exceptions;
using RevitGit.Domain.Snapshots;
using Xunit;

namespace RevitGit.Domain.Tests.Snapshots
{
    public sealed class ParameterValueTests
    {
        [Fact]
        public void SupportsAllV1ValueKinds()
        {
            var text = ParameterValue.FromString("Oak");
            var integer = ParameterValue.FromInteger(42);
            var number = ParameterValue.FromDouble(900.25);
            var boolean = ParameterValue.FromBoolean(true);
            var missing = ParameterValue.Null;

            Assert.Equal(ParameterValueKind.String, text.Kind);
            Assert.Equal("Oak", text.StringValue);
            Assert.Equal(ParameterValueKind.Integer, integer.Kind);
            Assert.Equal(42L, integer.IntegerValue);
            Assert.Equal(ParameterValueKind.Double, number.Kind);
            Assert.Equal(900.25, number.DoubleValue);
            Assert.Equal(ParameterValueKind.Boolean, boolean.Kind);
            Assert.True(boolean.BooleanValue.Value);
            Assert.Equal(ParameterValueKind.Null, missing.Kind);
        }

        [Fact]
        public void StringValuePreservesEmptyAndWhitespaceContent()
        {
            Assert.Equal("", ParameterValue.FromString("").StringValue);
            Assert.Equal("  ", ParameterValue.FromString("  ").StringValue);
        }

        [Fact]
        public void NullStringMustUseExplicitNullValue()
        {
            Assert.Throws<DomainException>(() => ParameterValue.FromString(null));
        }

        [Theory]
        [InlineData(900.00000001, 900.0)]
        [InlineData(1.2345674, 1.234567)]
        [InlineData(1.2345675, 1.234568)]
        [InlineData(-1.2345675, -1.234568)]
        public void DoubleValuesAreNormalizedToSixDecimalPlaces(double input, double expected)
        {
            Assert.Equal(expected, ParameterValue.FromDouble(input).DoubleValue.Value);
        }

        [Fact]
        public void NegativeZeroIsNormalizedToPositiveZero()
        {
            var value = ParameterValue.FromDouble(-0.0000001);

            Assert.Equal(0L, System.BitConverter.DoubleToInt64Bits(value.DoubleValue.Value));
        }

        [Theory]
        [InlineData(double.NaN)]
        [InlineData(double.PositiveInfinity)]
        [InlineData(double.NegativeInfinity)]
        public void NonFiniteDoubleIsRejected(double value)
        {
            Assert.Throws<DomainException>(() => ParameterValue.FromDouble(value));
        }

        [Fact]
        public void IndependentlyCreatedValuesHaveSemanticEqualityAndHashCodes()
        {
            var first = ParameterValue.FromDouble(900.00000001);
            var second = ParameterValue.FromDouble(900.0);

            Assert.Equal(first, second);
            Assert.Equal(first.GetHashCode(), second.GetHashCode());
            Assert.NotEqual(ParameterValue.FromInteger(900), first);
        }

        [Fact]
        public void CanonicalNumericPolicyIsExplicit()
        {
            Assert.Equal(6, SnapshotNumericPolicy.DecimalPlaces);
            Assert.Equal("millimeter", SnapshotNumericPolicy.LengthUnit);
            Assert.Equal("square millimeter", SnapshotNumericPolicy.AreaUnit);
            Assert.Equal("cubic millimeter", SnapshotNumericPolicy.VolumeUnit);
            Assert.Equal("degree", SnapshotNumericPolicy.AngleUnit);
        }
    }
}
