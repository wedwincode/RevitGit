using System.Collections.Generic;
using RevitGit.Application.Abstractions;
using RevitGit.Application.Models;
using RevitGit.Domain.History;

namespace RevitGit.Application.Tests.Fakes
{
    internal sealed class FakeHistoryRepository : IHistoryRepository
    {
        private readonly Dictionary<FamilyIdentity, FamilyHistory> _histories =
            new Dictionary<FamilyIdentity, FamilyHistory>();
        private readonly IList<string> _operations;

        public FakeHistoryRepository(IList<string> operations = null)
        {
            _operations = operations;
        }

        public int SaveCount { get; private set; }

        public bool Exists(FamilyIdentity familyIdentity)
        {
            return _histories.ContainsKey(familyIdentity);
        }

        public FamilyHistory Load(FamilyIdentity familyIdentity)
        {
            FamilyHistory history;
            return _histories.TryGetValue(familyIdentity, out history) ? history : null;
        }

        public void Save(FamilyIdentity familyIdentity, FamilyHistory history)
        {
            _operations?.Add("history-save");
            _histories[familyIdentity] = history;
            SaveCount++;
        }

        public void Seed(FamilyIdentity familyIdentity, FamilyHistory history)
        {
            _histories[familyIdentity] = history;
        }
    }
}
