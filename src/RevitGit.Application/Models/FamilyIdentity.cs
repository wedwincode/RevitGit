using System;

namespace RevitGit.Application.Models
{
    public sealed class FamilyIdentity : IEquatable<FamilyIdentity>
    {
        public FamilyIdentity(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Family identity cannot be empty.", nameof(value));
            }

            Value = value.Trim();
        }

        public string Value { get; }

        public bool Equals(FamilyIdentity other)
        {
            return other != null && string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as FamilyIdentity);
        }

        public override int GetHashCode()
        {
            return StringComparer.Ordinal.GetHashCode(Value);
        }

        public override string ToString()
        {
            return Value;
        }
    }
}
