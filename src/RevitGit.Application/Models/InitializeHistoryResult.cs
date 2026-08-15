namespace RevitGit.Application.Models
{
    public sealed class InitializeHistoryResult
    {
        public InitializeHistoryResult(bool wasCreated)
        {
            WasCreated = wasCreated;
        }

        public bool WasCreated { get; }
    }
}
