using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using RevitGit.Domain.Snapshots;

namespace RevitGit.Revit2021.Snapshots
{
    internal static class CanonicalGeometryFingerprint
    {
        public static string Compute(IEnumerable<string> descriptors)
        {
            var canonical = string.Join(
                "\n",
                descriptors.OrderBy(value => value, System.StringComparer.Ordinal));
            var bytes = Encoding.UTF8.GetBytes(canonical);

            using (var sha256 = SHA256.Create())
            {
                var hash = sha256.ComputeHash(bytes);
                var result = new StringBuilder(hash.Length * 2);
                foreach (var value in hash)
                {
                    result.Append(value.ToString("X2", CultureInfo.InvariantCulture));
                }

                return result.ToString();
            }
        }

        public static string Point(double x, double y, double z)
        {
            return Number(x) + "," + Number(y) + "," + Number(z);
        }

        public static string Number(double value)
        {
            return SnapshotNumericPolicy.Normalize(value).ToString(
                "0.######",
                CultureInfo.InvariantCulture);
        }
    }
}
