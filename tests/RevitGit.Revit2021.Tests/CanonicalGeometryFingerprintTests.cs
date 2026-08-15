using System.Globalization;
using RevitGit.Revit2021.Snapshots;
using Xunit;

namespace RevitGit.Revit2021.Tests
{
    public sealed class CanonicalGeometryFingerprintTests
    {
        [Fact]
        public void DescriptorOrderDoesNotAffectSha256()
        {
            var first = CanonicalGeometryFingerprint.Compute(new[] { "solid:b", "mesh:a" });
            var second = CanonicalGeometryFingerprint.Compute(new[] { "mesh:a", "solid:b" });

            Assert.Equal(first, second);
            Assert.Equal(64, first.Length);
            Assert.Matches("^[0-9A-F]{64}$", first);
        }

        [Fact]
        public void NoiseInsideSixDecimalPrecisionDoesNotDriftDescriptor()
        {
            Assert.Equal(
                CanonicalGeometryFingerprint.Point(1.0000001, -0.0000001, 3),
                CanonicalGeometryFingerprint.Point(1.0000004, 0, 3.0000001));
            Assert.NotEqual(
                CanonicalGeometryFingerprint.Point(1.0000004, 0, 3),
                CanonicalGeometryFingerprint.Point(1.0000006, 0, 3));
        }

        [Fact]
        public void CanonicalSerializationIsCultureIndependent()
        {
            var originalCulture = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ru-RU");
                var russian = CanonicalGeometryFingerprint.Compute(new[]
                {
                    CanonicalGeometryFingerprint.Point(1.25, 2.5, 3.75)
                });

                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
                var english = CanonicalGeometryFingerprint.Compute(new[]
                {
                    CanonicalGeometryFingerprint.Point(1.25, 2.5, 3.75)
                });

                Assert.Equal(russian, english);
            }
            finally
            {
                CultureInfo.CurrentCulture = originalCulture;
            }
        }
    }
}
