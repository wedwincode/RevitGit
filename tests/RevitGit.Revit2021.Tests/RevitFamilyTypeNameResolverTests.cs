using RevitGit.Revit2021.Snapshots;
using Xunit;

namespace RevitGit.Revit2021.Tests
{
    public sealed class RevitFamilyTypeNameResolverTests
    {
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void ResolveUsesFamilyNameForRevitImplicitType(string typeName)
        {
            var actual = RevitFamilyTypeNameResolver.Resolve(typeName, "Door");

            Assert.Equal("Door", actual);
        }

        [Fact]
        public void ResolvePreservesExplicitTypeName()
        {
            var actual = RevitFamilyTypeNameResolver.Resolve("  900 x 2100  ", "Door");

            Assert.Equal("900 x 2100", actual);
        }
    }
}
