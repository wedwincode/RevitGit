using RevitGit.Revit2021.Snapshots;
using Xunit;

namespace RevitGit.Revit2021.Tests
{
    public sealed class RevitFamilyIdentityResolverTests
    {
        [Fact]
        public void ResolveFamilyNameUsesOwnerFamilyNameWhenAvailable()
        {
            var actual = RevitFamilyIdentityResolver.ResolveFamilyName(
                "Owner family",
                "File name.rfa");

            Assert.Equal("Owner family", actual);
        }

        [Theory]
        [InlineData(null, "M_Trim-Window-Interior-Flat.rfa", "M_Trim-Window-Interior-Flat")]
        [InlineData("", "New Family", "New Family")]
        [InlineData("   ", "Upper.RFA", "Upper")]
        public void ResolveFamilyNameFallsBackToDocumentTitleWithoutRfaExtension(
            string ownerFamilyName,
            string documentTitle,
            string expected)
        {
            var actual = RevitFamilyIdentityResolver.ResolveFamilyName(ownerFamilyName, documentTitle);

            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(".rfa")]
        public void ResolveFamilyNameRejectsUnavailableIdentity(string documentTitle)
        {
            var exception = Assert.Throws<SnapshotExtractionException>(() =>
                RevitFamilyIdentityResolver.ResolveFamilyName(null, documentTitle));

            Assert.Contains("family name", exception.Message);
        }
    }
}
