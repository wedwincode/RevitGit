using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace RevitGit.Infrastructure.Git
{
    public sealed class RepositoryValidationResult
    {
        public RepositoryValidationResult(IEnumerable<RepositoryValidationIssue> issues)
        {
            Issues = new ReadOnlyCollection<RepositoryValidationIssue>(new List<RepositoryValidationIssue>(issues));
        }

        public bool IsValid => Issues.Count == 0;
        public IReadOnlyList<RepositoryValidationIssue> Issues { get; }
    }
}
