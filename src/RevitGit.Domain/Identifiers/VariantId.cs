using System;
using RevitGit.Domain.Exceptions;

namespace RevitGit.Domain.Identifiers
{
    public sealed class VariantId : IEquatable<VariantId>
    {
        public VariantId(Guid value)
        {
            if (value == Guid.Empty)
            {
                throw new DomainException("Variant identifier cannot be empty.");
            }

            Value = value;
        }

        public Guid Value { get; }

        public static VariantId New()
        {
            return new VariantId(Guid.NewGuid());
        }

        public bool Equals(VariantId other)
        {
            return other != null && Value.Equals(other.Value);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as VariantId);
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
