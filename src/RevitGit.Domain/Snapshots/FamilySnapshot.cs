using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using RevitGit.Domain.Exceptions;

namespace RevitGit.Domain.Snapshots
{
    public sealed class FamilySnapshot : IEquatable<FamilySnapshot>
    {
        public FamilySnapshot(
            string familyName,
            string category,
            IEnumerable<FamilyParameterSnapshot> parameters,
            IEnumerable<FamilyTypeSnapshot> types,
            string geometryFingerprint)
        {
            if (string.IsNullOrWhiteSpace(familyName))
            {
                throw new DomainException("Family name cannot be empty.");
            }

            if (string.IsNullOrWhiteSpace(category))
            {
                throw new DomainException("Family category cannot be empty.");
            }

            if (parameters == null)
            {
                throw new DomainException("Family parameters are required.");
            }

            if (types == null)
            {
                throw new DomainException("Family types are required.");
            }

            if (geometryFingerprint != null && string.IsNullOrWhiteSpace(geometryFingerprint))
            {
                throw new DomainException("Geometry fingerprint cannot be empty when provided.");
            }

            var canonicalParameters = CanonicalizeParameters(parameters);
            var canonicalTypes = CanonicalizeTypes(types);
            ValidateTypeValueKeys(canonicalParameters, canonicalTypes);

            SchemaVersion = SnapshotSchema.CurrentVersion;
            FamilyName = familyName.Trim();
            Category = category.Trim();
            Parameters = new ReadOnlyCollection<FamilyParameterSnapshot>(canonicalParameters);
            Types = new ReadOnlyCollection<FamilyTypeSnapshot>(canonicalTypes);
            GeometryFingerprint = geometryFingerprint == null ? null : geometryFingerprint.Trim();
        }

        public int SchemaVersion { get; }

        public string FamilyName { get; }

        public string Category { get; }

        public IReadOnlyList<FamilyParameterSnapshot> Parameters { get; }

        public IReadOnlyList<FamilyTypeSnapshot> Types { get; }

        public string GeometryFingerprint { get; }

        public bool Equals(FamilySnapshot other)
        {
            return other != null
                   && SchemaVersion == other.SchemaVersion
                   && string.Equals(FamilyName, other.FamilyName, StringComparison.Ordinal)
                   && string.Equals(Category, other.Category, StringComparison.Ordinal)
                   && string.Equals(GeometryFingerprint, other.GeometryFingerprint, StringComparison.Ordinal)
                   && SequenceEqual(Parameters, other.Parameters)
                   && SequenceEqual(Types, other.Types);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as FamilySnapshot);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = SchemaVersion;
                hashCode = (hashCode * 397) ^ StringComparer.Ordinal.GetHashCode(FamilyName);
                hashCode = (hashCode * 397) ^ StringComparer.Ordinal.GetHashCode(Category);
                hashCode = (hashCode * 397) ^ (GeometryFingerprint == null
                    ? 0
                    : StringComparer.Ordinal.GetHashCode(GeometryFingerprint));

                foreach (var parameter in Parameters)
                {
                    hashCode = (hashCode * 397) ^ parameter.GetHashCode();
                }

                foreach (var type in Types)
                {
                    hashCode = (hashCode * 397) ^ type.GetHashCode();
                }

                return hashCode;
            }
        }

        private static List<FamilyParameterSnapshot> CanonicalizeParameters(
            IEnumerable<FamilyParameterSnapshot> parameters)
        {
            var result = new List<FamilyParameterSnapshot>();
            var keys = new HashSet<string>(StringComparer.Ordinal);
            var names = new HashSet<string>(StringComparer.Ordinal);

            foreach (var parameter in parameters)
            {
                if (parameter == null)
                {
                    throw new DomainException("Family parameters cannot contain null values.");
                }

                if (!keys.Add(parameter.StableKey))
                {
                    throw new DomainException("Family contains duplicate parameter stable key: " + parameter.StableKey + ".");
                }

                if (!names.Add(parameter.Name))
                {
                    throw new DomainException("Family contains duplicate parameter name: " + parameter.Name + ".");
                }

                result.Add(parameter);
            }

            result.Sort(CompareParameters);
            return result;
        }

        private static List<FamilyTypeSnapshot> CanonicalizeTypes(IEnumerable<FamilyTypeSnapshot> types)
        {
            var result = new List<FamilyTypeSnapshot>();
            var names = new HashSet<string>(StringComparer.Ordinal);

            foreach (var type in types)
            {
                if (type == null)
                {
                    throw new DomainException("Family types cannot contain null values.");
                }

                if (!names.Add(type.Name))
                {
                    throw new DomainException("Family contains duplicate type name: " + type.Name + ".");
                }

                result.Add(type);
            }

            result.Sort((left, right) => StringComparer.Ordinal.Compare(left.Name, right.Name));
            return result;
        }

        private static void ValidateTypeValueKeys(
            IEnumerable<FamilyParameterSnapshot> parameters,
            IEnumerable<FamilyTypeSnapshot> types)
        {
            var parameterKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var parameter in parameters)
            {
                parameterKeys.Add(parameter.StableKey);
            }

            foreach (var type in types)
            {
                foreach (var key in type.Values.Keys)
                {
                    if (!parameterKeys.Contains(key))
                    {
                        throw new DomainException(
                            "Family type " + type.Name + " contains a value for unknown parameter key: " + key + ".");
                    }
                }
            }
        }

        private static int CompareParameters(FamilyParameterSnapshot left, FamilyParameterSnapshot right)
        {
            var byName = StringComparer.Ordinal.Compare(left.Name, right.Name);
            return byName != 0
                ? byName
                : StringComparer.Ordinal.Compare(left.StableKey, right.StableKey);
        }

        private static bool SequenceEqual<T>(IReadOnlyList<T> left, IReadOnlyList<T> right)
        {
            if (left.Count != right.Count)
            {
                return false;
            }

            var comparer = EqualityComparer<T>.Default;
            for (var index = 0; index < left.Count; index++)
            {
                if (!comparer.Equals(left[index], right[index]))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
