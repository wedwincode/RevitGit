using System;
using System.Collections.Generic;
using RevitGit.Domain.Snapshots;

namespace RevitGit.Application.Diff
{
    public sealed class FamilyDiffEngine
    {
        public FamilyDiff Compare(FamilySnapshot before, FamilySnapshot after)
        {
            if (before == null)
            {
                throw new ArgumentNullException(nameof(before));
            }

            if (after == null)
            {
                throw new ArgumentNullException(nameof(after));
            }

            return new FamilyDiff(
                CreateStringChange(before.FamilyName, after.FamilyName),
                CreateStringChange(before.Category, after.Category),
                CompareParameters(before.Parameters, after.Parameters),
                CompareTypes(before, after),
                !string.Equals(
                    before.GeometryFingerprint,
                    after.GeometryFingerprint,
                    StringComparison.Ordinal));
        }

        private static IList<FamilyTypeChange> CompareTypes(FamilySnapshot before, FamilySnapshot after)
        {
            var beforeByName = IndexTypes(before.Types);
            var afterByName = IndexTypes(after.Types);
            var parameterNames = IndexParameterNames(before.Parameters, after.Parameters);
            var parameterDataTypes = IndexParameterDataTypes(before.Parameters, after.Parameters);
            var allNames = new SortedSet<string>(beforeByName.Keys, StringComparer.Ordinal);
            allNames.UnionWith(afterByName.Keys);

            var changes = new List<FamilyTypeChange>();
            foreach (var name in allNames)
            {
                FamilyTypeSnapshot oldType;
                FamilyTypeSnapshot newType;
                var existed = beforeByName.TryGetValue(name, out oldType);
                var exists = afterByName.TryGetValue(name, out newType);

                if (!existed)
                {
                    changes.Add(new FamilyTypeChange(
                        name, ChangeKind.Added, null, newType, new ParameterValueChange[0]));
                }
                else if (!exists)
                {
                    changes.Add(new FamilyTypeChange(
                        name, ChangeKind.Removed, oldType, null, new ParameterValueChange[0]));
                }
                else if (!oldType.Equals(newType))
                {
                    changes.Add(new FamilyTypeChange(
                        name,
                        ChangeKind.Modified,
                        oldType,
                        newType,
                        CompareParameterValues(oldType.Values, newType.Values, parameterNames, parameterDataTypes)));
                }
            }

            return changes;
        }

        private static IList<ParameterValueChange> CompareParameterValues(
            IReadOnlyDictionary<string, ParameterValue> before,
            IReadOnlyDictionary<string, ParameterValue> after,
            IReadOnlyDictionary<string, string> parameterNames,
            IReadOnlyDictionary<string, ParameterDataType> parameterDataTypes)
        {
            var allKeys = new SortedSet<string>(before.Keys, StringComparer.Ordinal);
            allKeys.UnionWith(after.Keys);
            var changes = new List<ParameterValueChange>();

            foreach (var key in allKeys)
            {
                ParameterValue oldValue;
                ParameterValue newValue;
                var existed = before.TryGetValue(key, out oldValue);
                var exists = after.TryGetValue(key, out newValue);

                if (!existed)
                {
                    changes.Add(new ParameterValueChange(
                        key, parameterNames[key], parameterDataTypes[key], ChangeKind.Added, null, newValue));
                }
                else if (!exists)
                {
                    changes.Add(new ParameterValueChange(
                        key, parameterNames[key], parameterDataTypes[key], ChangeKind.Removed, oldValue, null));
                }
                else if (!oldValue.Equals(newValue))
                {
                    changes.Add(new ParameterValueChange(
                        key, parameterNames[key], parameterDataTypes[key], ChangeKind.Modified, oldValue, newValue));
                }
            }

            changes.Sort(CompareParameterValueChanges);
            return changes;
        }

        private static IList<ParameterChange> CompareParameters(
            IEnumerable<FamilyParameterSnapshot> before,
            IEnumerable<FamilyParameterSnapshot> after)
        {
            var beforeByKey = IndexParameters(before);
            var afterByKey = IndexParameters(after);
            var allKeys = new SortedSet<string>(beforeByKey.Keys, StringComparer.Ordinal);
            allKeys.UnionWith(afterByKey.Keys);

            var changes = new List<ParameterChange>();
            foreach (var key in allKeys)
            {
                FamilyParameterSnapshot oldParameter;
                FamilyParameterSnapshot newParameter;
                var existed = beforeByKey.TryGetValue(key, out oldParameter);
                var exists = afterByKey.TryGetValue(key, out newParameter);

                if (!existed)
                {
                    changes.Add(new ParameterChange(
                        key, newParameter.Name, ChangeKind.Added, null, newParameter,
                        null, null, null, null));
                }
                else if (!exists)
                {
                    changes.Add(new ParameterChange(
                        key, oldParameter.Name, ChangeKind.Removed, oldParameter, null,
                        null, null, null, null));
                }
                else if (!oldParameter.Equals(newParameter))
                {
                    changes.Add(new ParameterChange(
                        key,
                        newParameter.Name,
                        ChangeKind.Modified,
                        oldParameter,
                        newParameter,
                        CreateStringChange(oldParameter.Name, newParameter.Name),
                        oldParameter.DataType == newParameter.DataType
                            ? null
                            : new ValueChange<ParameterDataType>(oldParameter.DataType, newParameter.DataType),
                        oldParameter.Scope == newParameter.Scope
                            ? null
                            : new ValueChange<ParameterScope>(oldParameter.Scope, newParameter.Scope),
                        CreateStringChange(oldParameter.Formula, newParameter.Formula)));
                }
            }

            changes.Sort(CompareParameterChanges);
            return changes;
        }

        private static Dictionary<string, FamilyParameterSnapshot> IndexParameters(
            IEnumerable<FamilyParameterSnapshot> parameters)
        {
            var result = new Dictionary<string, FamilyParameterSnapshot>(StringComparer.Ordinal);
            foreach (var parameter in parameters)
            {
                result.Add(parameter.StableKey, parameter);
            }

            return result;
        }

        private static Dictionary<string, FamilyTypeSnapshot> IndexTypes(
            IEnumerable<FamilyTypeSnapshot> types)
        {
            var result = new Dictionary<string, FamilyTypeSnapshot>(StringComparer.Ordinal);
            foreach (var type in types)
            {
                result.Add(type.Name, type);
            }

            return result;
        }

        private static Dictionary<string, string> IndexParameterNames(
            IEnumerable<FamilyParameterSnapshot> before,
            IEnumerable<FamilyParameterSnapshot> after)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var parameter in before)
            {
                result[parameter.StableKey] = parameter.Name;
            }

            foreach (var parameter in after)
            {
                result[parameter.StableKey] = parameter.Name;
            }

            return result;
        }

        private static Dictionary<string, ParameterDataType> IndexParameterDataTypes(
            IEnumerable<FamilyParameterSnapshot> before,
            IEnumerable<FamilyParameterSnapshot> after)
        {
            var result = new Dictionary<string, ParameterDataType>(StringComparer.Ordinal);
            foreach (var parameter in before) result[parameter.StableKey] = parameter.DataType;
            foreach (var parameter in after) result[parameter.StableKey] = parameter.DataType;
            return result;
        }

        private static ValueChange<string> CreateStringChange(string oldValue, string newValue)
        {
            return string.Equals(oldValue, newValue, StringComparison.Ordinal)
                ? null
                : new ValueChange<string>(oldValue, newValue);
        }

        private static int CompareParameterChanges(ParameterChange left, ParameterChange right)
        {
            var byName = StringComparer.Ordinal.Compare(left.Name, right.Name);
            return byName != 0
                ? byName
                : StringComparer.Ordinal.Compare(left.StableKey, right.StableKey);
        }

        private static int CompareParameterValueChanges(
            ParameterValueChange left,
            ParameterValueChange right)
        {
            var byName = StringComparer.Ordinal.Compare(left.ParameterName, right.ParameterName);
            return byName != 0
                ? byName
                : StringComparer.Ordinal.Compare(left.ParameterStableKey, right.ParameterStableKey);
        }
    }
}
