using System;
using RevitGit.Domain.Exceptions;

namespace RevitGit.Domain.Snapshots
{
    public sealed class FamilyParameterSnapshot : IEquatable<FamilyParameterSnapshot>
    {
        public FamilyParameterSnapshot(
            string stableKey,
            string name,
            ParameterDataType dataType,
            ParameterScope scope,
            string formula)
        {
            if (string.IsNullOrWhiteSpace(stableKey))
            {
                throw new DomainException("Parameter stable key cannot be empty.");
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                throw new DomainException("Parameter name cannot be empty.");
            }

            if (!Enum.IsDefined(typeof(ParameterDataType), dataType))
            {
                throw new DomainException("Parameter data type is invalid.");
            }

            if (!Enum.IsDefined(typeof(ParameterScope), scope))
            {
                throw new DomainException("Parameter scope is invalid.");
            }

            StableKey = stableKey.Trim();
            Name = name.Trim();
            DataType = dataType;
            Scope = scope;
            Formula = string.IsNullOrWhiteSpace(formula) ? null : formula.Trim();
        }

        public string StableKey { get; }

        public string Name { get; }

        public ParameterDataType DataType { get; }

        public ParameterScope Scope { get; }

        public string Formula { get; }

        public bool Equals(FamilyParameterSnapshot other)
        {
            return other != null
                   && string.Equals(StableKey, other.StableKey, StringComparison.Ordinal)
                   && string.Equals(Name, other.Name, StringComparison.Ordinal)
                   && DataType == other.DataType
                   && Scope == other.Scope
                   && string.Equals(Formula, other.Formula, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as FamilyParameterSnapshot);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = StringComparer.Ordinal.GetHashCode(StableKey);
                hashCode = (hashCode * 397) ^ StringComparer.Ordinal.GetHashCode(Name);
                hashCode = (hashCode * 397) ^ (int)DataType;
                hashCode = (hashCode * 397) ^ (int)Scope;
                hashCode = (hashCode * 397) ^ (Formula == null ? 0 : StringComparer.Ordinal.GetHashCode(Formula));
                return hashCode;
            }
        }
    }
}
