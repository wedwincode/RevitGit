namespace RevitGit.Infrastructure.FileSystem.Exceptions
{
    public sealed class RepositoryNotFoundException : StorageException
    {
        public RepositoryNotFoundException(string familyFileName)
            : base("No family history repository is associated with " + familyFileName + ".")
        {
        }
    }
}
