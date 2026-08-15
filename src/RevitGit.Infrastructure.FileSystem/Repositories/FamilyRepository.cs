namespace RevitGit.Infrastructure.FileSystem.Repositories
{
    public sealed class FamilyRepository
    {
        internal FamilyRepository(FamilyRepositoryPaths paths, FamilyRepositoryMetadata metadata)
        {
            Paths = paths;
            Metadata = metadata;
        }

        public FamilyRepositoryPaths Paths { get; }
        public FamilyRepositoryMetadata Metadata { get; }
    }
}
