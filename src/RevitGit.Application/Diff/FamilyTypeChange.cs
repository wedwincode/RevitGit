using System.Collections.Generic;
using System.Collections.ObjectModel;
using RevitGit.Domain.Snapshots;

namespace RevitGit.Application.Diff
{
    public sealed class FamilyTypeChange
    {
        internal FamilyTypeChange(
            string name,
            ChangeKind kind,
            FamilyTypeSnapshot before,
            FamilyTypeSnapshot after,
            IEnumerable<ParameterValueChange> valueChanges)
        {
            Name = name;
            Kind = kind;
            Before = before;
            After = after;
            ValueChanges = new ReadOnlyCollection<ParameterValueChange>(
                new List<ParameterValueChange>(valueChanges));
        }

        public string Name { get; }

        public ChangeKind Kind { get; }

        public FamilyTypeSnapshot Before { get; }

        public FamilyTypeSnapshot After { get; }

        public IReadOnlyList<ParameterValueChange> ValueChanges { get; }
    }
}
