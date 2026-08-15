using System;
using RevitGit.Domain.Exceptions;
using RevitGit.Domain.Identifiers;
using Xunit;

namespace RevitGit.Domain.Tests.Identifiers
{
    public sealed class IdentifierTests
    {
        [Fact]
        public void VersionIdsWithSameValueAreEqual()
        {
            var value = Guid.Parse("11111111-1111-1111-1111-111111111111");

            Assert.Equal(new VersionId(value), new VersionId(value));
        }

        [Fact]
        public void VariantIdsWithSameValueAreEqual()
        {
            var value = Guid.Parse("22222222-2222-2222-2222-222222222222");

            Assert.Equal(new VariantId(value), new VariantId(value));
        }

        [Fact]
        public void DifferentIdentifierValuesAreNotEqual()
        {
            Assert.NotEqual(
                new VersionId(Guid.Parse("11111111-1111-1111-1111-111111111111")),
                new VersionId(Guid.Parse("33333333-3333-3333-3333-333333333333")));
            Assert.NotEqual(
                new VariantId(Guid.Parse("22222222-2222-2222-2222-222222222222")),
                new VariantId(Guid.Parse("44444444-4444-4444-4444-444444444444")));
        }

        [Fact]
        public void EmptyIdentifierValueIsRejected()
        {
            Assert.Throws<DomainException>(() => new VersionId(Guid.Empty));
            Assert.Throws<DomainException>(() => new VariantId(Guid.Empty));
        }
    }
}
