using RevitGit.Domain.Snapshots;
using RevitGit.Revit2021.Snapshots;
using Xunit;

namespace RevitGit.Revit2021.Tests
{
    public sealed class RevitParameterValuePolicyTests
    {
        [Fact]
        public void YesNoIntegerBecomesSemanticBoolean()
        {
            Assert.Equal(ParameterValue.FromBoolean(false), RevitParameterValuePolicy.FromInteger(0, ParameterDataType.YesNo));
            Assert.Equal(ParameterValue.FromBoolean(true), RevitParameterValuePolicy.FromInteger(1, ParameterDataType.YesNo));
        }

        [Fact]
        public void OrdinaryIntegerRemainsInteger()
        {
            Assert.Equal(ParameterValue.FromInteger(42), RevitParameterValuePolicy.FromInteger(42, ParameterDataType.Integer));
        }

        [Fact]
        public void MissingIntegerBecomesExplicitNull()
        {
            Assert.Same(ParameterValue.Null, RevitParameterValuePolicy.FromInteger(null, ParameterDataType.YesNo));
        }

        [Fact]
        public void ReferenceFormattingUsesNamesAndNeverNumericIds()
        {
            Assert.Equal("Oak", RevitReferenceValueFormatter.Material("Oak").StringValue);
            Assert.Equal("Handle : Long", RevitReferenceValueFormatter.FamilyType("Handle", "Long").StringValue);
            Assert.Equal("Named element", RevitReferenceValueFormatter.NamedElement("Named element").StringValue);
            Assert.Same(ParameterValue.Null, RevitReferenceValueFormatter.NamedElement(null));
        }

        [Fact]
        public void FormulaIsPassedToSnapshotContractWithoutExpressionRewriting()
        {
            var snapshot = RevitParameterValuePolicy.CreateParameterSnapshot(
                "family:offset",
                "Offset",
                ParameterDataType.Length,
                ParameterScope.Type,
                " Width / 2 + Offset ");

            Assert.Equal("Width / 2 + Offset", snapshot.Formula);
        }
    }
}
