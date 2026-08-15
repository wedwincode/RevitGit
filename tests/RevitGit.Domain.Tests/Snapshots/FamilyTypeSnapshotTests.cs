using System;
using System.Collections.Generic;
using System.Linq;
using RevitGit.Domain.Exceptions;
using RevitGit.Domain.Snapshots;
using Xunit;

namespace RevitGit.Domain.Tests.Snapshots
{
    public sealed class FamilyTypeSnapshotTests
    {
        [Fact]
        public void NameIsTrimmedAndValuesHaveCanonicalOrder()
        {
            var type = new FamilyTypeSnapshot(
                " 900x2100 ",
                new Dictionary<string, ParameterValue>
                {
                    { "family:width", ParameterValue.FromDouble(900) },
                    { "family:height", ParameterValue.FromDouble(2100) }
                });

            Assert.Equal("900x2100", type.Name);
            Assert.Equal(new[] { "family:height", "family:width" }, type.Values.Keys.ToArray());
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void NameCannotBeEmpty(string name)
        {
            Assert.Throws<DomainException>(
                () => new FamilyTypeSnapshot(name, new Dictionary<string, ParameterValue>()));
        }

        [Fact]
        public void NullValuesCollectionIsRejected()
        {
            Assert.Throws<DomainException>(() => new FamilyTypeSnapshot("Default", null));
        }

        [Fact]
        public void ValueKeysAreTrimmedAndDuplicatesAfterNormalizationAreRejected()
        {
            var values = new Dictionary<string, ParameterValue>
            {
                { "family:width", ParameterValue.FromDouble(900) },
                { " family:width ", ParameterValue.FromDouble(1000) }
            };

            Assert.Throws<DomainException>(() => new FamilyTypeSnapshot("Default", values));
        }

        [Fact]
        public void ValuesAreDefensivelyCopiedAndExternallyReadOnly()
        {
            var source = new Dictionary<string, ParameterValue>
            {
                { "family:width", ParameterValue.FromDouble(900) }
            };
            var type = new FamilyTypeSnapshot("Default", source);

            source["family:width"] = ParameterValue.FromDouble(1000);
            var published = (IDictionary<string, ParameterValue>)type.Values;

            Assert.Equal(ParameterValue.FromDouble(900), type.Values["family:width"]);
            Assert.Throws<NotSupportedException>(
                () => published["family:width"] = ParameterValue.FromDouble(1200));
        }

        [Fact]
        public void ValueInputOrderDoesNotAffectEqualityOrHashCode()
        {
            var first = new FamilyTypeSnapshot("Default", Values(900, 2100));
            var second = new FamilyTypeSnapshot("Default", ValuesReversed(900, 2100));

            Assert.Equal(first, second);
            Assert.Equal(first.GetHashCode(), second.GetHashCode());
        }

        private static IDictionary<string, ParameterValue> Values(double width, double height)
        {
            return new Dictionary<string, ParameterValue>
            {
                { "family:width", ParameterValue.FromDouble(width) },
                { "family:height", ParameterValue.FromDouble(height) }
            };
        }

        private static IDictionary<string, ParameterValue> ValuesReversed(double width, double height)
        {
            return new Dictionary<string, ParameterValue>
            {
                { "family:height", ParameterValue.FromDouble(height) },
                { "family:width", ParameterValue.FromDouble(width) }
            };
        }
    }
}
