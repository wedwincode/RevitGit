using System;
using System.IO;

namespace RevitGit.Infrastructure.FileSystem.IO
{
    internal sealed class AtomicFileWriter
    {
        public void Write(string targetPath, byte[] content)
        {
            var directory = Path.GetDirectoryName(targetPath);
            Directory.CreateDirectory(directory);
            var temporaryPath = Path.Combine(directory, ".tmp-" + Guid.NewGuid().ToString("N"));

            try
            {
                using (var stream = new FileStream(
                    temporaryPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    4096,
                    FileOptions.WriteThrough))
                {
                    stream.Write(content, 0, content.Length);
                    stream.Flush(true);
                }

                if (File.Exists(targetPath))
                {
                    File.Replace(temporaryPath, targetPath, null);
                }
                else
                {
                    File.Move(temporaryPath, targetPath);
                }
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
        }
    }
}
