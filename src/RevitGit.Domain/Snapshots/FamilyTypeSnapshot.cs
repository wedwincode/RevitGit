using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using RevitGit.Domain.Exceptions;

namespace RevitGit.Domain.Snapshots
{
    public sealed class FamilyTypeSnapshot : IEquatable<FamilyTypeSnapshot>
    {
        public FamilyTypeSnapshot(
            string name,
            IEnumerable<KeyValuePair<string, ParameterValue>> values)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new DomainException("Family type name cannot be empty.");
            }

            if (values == null)
            {
                throw new DomainException("Family type values are required.");
            }

            var canonicalValues = new SortedDictionary<string, ParameterValue>(StringComparer.Ordinal);
            foreach (var pair in values)
            {
                if (string.IsNullOrWhiteSpace(pair.Key))
                {
                    throw new DomainException("Family type value parameter key cannot be empty.");
                }

                if (pair.Value == null)
                {
                    throw new DomainException("Family type parameter values cannot be null. Use ParameterValue.Null instead.");
                }

                var key = pair.Key.Trim();
                if (canonicalValues.ContainsKey(key))
                {
                    throw new DomainException("Family type contains duplicate parameter value key: " + key + ".");
                }

                canonicalValues.Add(key, pair.Value);
            }

            Name = name.Trim();
            Values = new ReadOnlyDictionary<string, ParameterValue>(canonicalValues);
        }

        public string Name { get; }

        public IReadOnlyDictionary<string, ParameterValue> Values { get; }

        public bool Equals(FamilyTypeSnapshot other)
        {
            if (other == null
                || !string.Equals(Name, other.Name, StringComparison.Ordinal)
                || Values.Count != other.Values.Count)
            {
                return false;
            }

            using (var left = Values.GetEnumerator())
            using (var right = other.Values.GetEnumerator())
            {
                while (left.MoveNext() && right.MoveNext())
                {
                    if (!string.Equals(left.Current.Key, right.Current.Key, StringComparison.Ordinal)
                        || !left.Current.Value.Equals(right.Current.Value))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as FamilyTypeSnapshot);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = StringComparer.Ordinal.GetHashCode(Name);
                foreach (var pair in Values)
                {
                    hashCode = (hashCode * 397) ^ StringComparer.Ordinal.GetHashCode(pair.Key);
                    hashCode = (hashCode * 397) ^ pair.Value.GetHashCode();
                }

                return hashCode;
            }
        }
    }
}
