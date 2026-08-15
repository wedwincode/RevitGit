using System;
using System.IO;

namespace RevitGit.Infrastructure.FileSystem.Tests.Helpers
{
    internal sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "RevitGit.Tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public string CreateFamily(string fileName = "Door.rfa", byte[] content = null)
        {
            var path = System.IO.Path.Combine(Path, fileName);
            File.WriteAllBytes(path, content ?? new byte[] { 1, 2, 3 });
            return path;
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, true);
            }
        }
    }
}
