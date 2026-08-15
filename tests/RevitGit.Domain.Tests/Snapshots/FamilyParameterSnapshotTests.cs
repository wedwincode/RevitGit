using RevitGit.Domain.Exceptions;
using RevitGit.Domain.Snapshots;
using Xunit;

namespace RevitGit.Domain.Tests.Snapshots
{
    public sealed class FamilyParameterSnapshotTests
    {
        [Fact]
        public void ConstructorNormalizesStableKeyNameAndFormula()
        {
            var parameter = new FamilyParameterSnapshot(
                " shared:width ",
                " Width ",
                ParameterDataType.Length,
                ParameterScope.Instance,
                " Width / 2 ");

            Assert.Equal("shared:width", parameter.StableKey);
            Assert.Equal("Width", parameter.Name);
            Assert.Equal("Width / 2", parameter.Formula);
            Assert.Equal(ParameterDataType.Length, parameter.DataType);
            Assert.Equal(ParameterScope.Instance, parameter.Scope);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void StableKeyCannotBeEmpty(string stableKey)
        {
            Assert.Throws<DomainException>(() => Create(stableKey, "Width"));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void NameCannotBeEmpty(string name)
        {
            Assert.Throws<DomainException>(() => Create("family:width", name));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void EmptyFormulaIsNormalizedToNull(string formula)
        {
            Assert.Null(new FamilyParameterSnapshot(
                "family:width",
                "Width",
                ParameterDataType.Length,
                ParameterScope.Type,
                formula).Formula);
        }

        [Fact]
        public void InstanceAndTypeScopesRemainDistinct()
        {
            var instance = Create("family:width", "Width", ParameterScope.Instance);
            var type = Create("family:width", "Width", ParameterScope.Type);

            Assert.NotEqual(instance, type);
        }

        [Fact]
        public void UnknownDataTypeIsSupportedAndDataTypesRemainDistinct()
        {
            var unknown = new FamilyParameterSnapshot(
                "family:value", "Value", ParameterDataType.Unknown, ParameterScope.Type, null);
            var number = new FamilyParameterSnapshot(
                "family:value", "Value", ParameterDataType.Number, ParameterScope.Type, null);

            Assert.Equal(ParameterDataType.Unknown, unknown.DataType);
            Assert.NotEqual(unknown, number);
        }

        private static FamilyParameterSnapshot Create(
            string stableKey,
            string name,
            ParameterScope scope = ParameterScope.Type)
        {
            return new FamilyParameterSnapshot(
                stableKey,
                name,
                ParameterDataType.Length,
                scope,
                null);
        }
    }
}
