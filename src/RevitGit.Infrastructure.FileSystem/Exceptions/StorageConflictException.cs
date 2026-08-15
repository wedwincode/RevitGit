namespace RevitGit.Infrastructure.FileSystem.Exceptions
{
    public sealed class StorageConflictException : StorageException
    {
        public StorageConflictException(string message)
            : base(message)
        {
        }
    }
}
