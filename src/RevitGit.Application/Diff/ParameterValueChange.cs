using RevitGit.Domain.Snapshots;

namespace RevitGit.Application.Diff
{
    public sealed class ParameterValueChange
    {
        internal ParameterValueChange(
            string parameterStableKey,
            string parameterName,
            ChangeKind kind,
            ParameterValue before,
            ParameterValue after)
        {
            ParameterStableKey = parameterStableKey;
            ParameterName = parameterName;
            Kind = kind;
            Before = before;
            After = after;
        }

        public string ParameterStableKey { get; }

        public string ParameterName { get; }

        public ChangeKind Kind { get; }

        public ParameterValue Before { get; }

        public ParameterValue After { get; }
    }
}
