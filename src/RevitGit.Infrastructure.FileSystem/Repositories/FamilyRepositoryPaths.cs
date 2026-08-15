namespace RevitGit.Infrastructure.FileSystem.Repositories
{
    public sealed class FamilyRepositoryPaths
    {
        internal FamilyRepositoryPaths(
            string familyFilePath,
            string historyRoot,
            string indexPath,
            string repositoryDirectory,
            string metadataPath,
            string historyPath,
            string versionsDirectory)
        {
            FamilyFilePath = familyFilePath;
            HistoryRoot = historyRoot;
            IndexPath = indexPath;
            RepositoryDirectory = repositoryDirectory;
            MetadataPath = metadataPath;
            HistoryPath = historyPath;
            VersionsDirectory = versionsDirectory;
        }

        public string FamilyFilePath { get; }
        public string HistoryRoot { get; }
        public string IndexPath { get; }
        public string RepositoryDirectory { get; }
        public string MetadataPath { get; }
        public string HistoryPath { get; }
        public string VersionsDirectory { get; }
    }
}
