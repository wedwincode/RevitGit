using RevitGit.Domain.Snapshots;

namespace RevitGit.Application.Diff
{
    public sealed class ParameterChange
    {
        internal ParameterChange(
            string stableKey,
            string name,
            ChangeKind kind,
            FamilyParameterSnapshot before,
            FamilyParameterSnapshot after,
            ValueChange<string> nameChange,
            ValueChange<ParameterDataType> dataTypeChange,
            ValueChange<ParameterScope> scopeChange,
            ValueChange<string> formulaChange)
        {
            StableKey = stableKey;
            Name = name;
            Kind = kind;
            Before = before;
            After = after;
            NameChange = nameChange;
            DataTypeChange = dataTypeChange;
            ScopeChange = scopeChange;
            FormulaChange = formulaChange;
        }

        public string StableKey { get; }

        public string Name { get; }

        public ChangeKind Kind { get; }

        public FamilyParameterSnapshot Before { get; }

        public FamilyParameterSnapshot After { get; }

        public ValueChange<string> NameChange { get; }

        public ValueChange<ParameterDataType> DataTypeChange { get; }

        public ValueChange<ParameterScope> ScopeChange { get; }

        public ValueChange<string> FormulaChange { get; }
    }
}
