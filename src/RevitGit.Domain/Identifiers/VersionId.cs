using System;
using RevitGit.Domain.Exceptions;

namespace RevitGit.Domain.Identifiers
{
    public sealed class VersionId : IEquatable<VersionId>
    {
        public VersionId(Guid value)
        {
            if (value == Guid.Empty)
            {
                throw new DomainException("Version identifier cannot be empty.");
            }

            Value = value;
        }

        public Guid Value { get; }

        public static VersionId New()
        {
            return new VersionId(Guid.NewGuid());
        }

        public bool Equals(VersionId other)
        {
            return other != null && Value.Equals(other.Value);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as VersionId);
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        public override string ToString()
        {
            return Value.ToString();
        }
    }
}
