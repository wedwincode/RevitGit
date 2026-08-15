using System;

namespace RevitGit.Infrastructure.Git
{
    public class GitStorageException : Exception
    {
        public GitStorageException(string message) : base(message)
        {
        }

        public GitStorageException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
