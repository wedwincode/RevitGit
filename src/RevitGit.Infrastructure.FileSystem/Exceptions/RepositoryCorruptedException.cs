using System;

namespace RevitGit.Infrastructure.FileSystem.Exceptions
{
    public sealed class RepositoryCorruptedException : StorageException
    {
        public RepositoryCorruptedException(string message)
            : base(message)
        {
        }

        public RepositoryCorruptedException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
