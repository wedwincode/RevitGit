using System;

namespace RevitGit.Infrastructure.Git
{
    public sealed class GitRepositoryCorruptedException : GitStorageException
    {
        public GitRepositoryCorruptedException(string message) : base(message)
        {
        }

        public GitRepositoryCorruptedException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
