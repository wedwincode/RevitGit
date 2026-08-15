using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace RevitGit.Infrastructure.FileSystem.Integrity
{
    internal sealed class ChecksumService
    {
        public string ComputeSha256(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var algorithm = SHA256.Create())
            {
                var hash = algorithm.ComputeHash(stream);
                var result = new StringBuilder(hash.Length * 2);
                foreach (var value in hash)
                {
                    result.Append(value.ToString("x2"));
                }

                return result.ToString();
            }
        }
    }
}
