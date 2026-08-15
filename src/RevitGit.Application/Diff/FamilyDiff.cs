using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace RevitGit.Application.Diff
{
    public sealed class FamilyDiff
    {
        internal FamilyDiff(
            ValueChange<string> familyNameChange,
            ValueChange<string> categoryChange,
            IEnumerable<ParameterChange> parameterChanges,
            IEnumerable<FamilyTypeChange> typeChanges,
            bool geometryChanged)
        {
            FamilyNameChange = familyNameChange;
            CategoryChange = categoryChange;
            ParameterChanges = new ReadOnlyCollection<ParameterChange>(
                new List<ParameterChange>(parameterChanges));
            TypeChanges = new ReadOnlyCollection<FamilyTypeChange>(
                new List<FamilyTypeChange>(typeChanges));
            GeometryChanged = geometryChanged;
        }

        public ValueChange<string> FamilyNameChange { get; }

        public ValueChange<string> CategoryChange { get; }

        public IReadOnlyList<ParameterChange> ParameterChanges { get; }

        public IReadOnlyList<FamilyTypeChange> TypeChanges { get; }

        public bool GeometryChanged { get; }

        public bool HasChanges
        {
            get
            {
                return FamilyNameChange != null
                       || CategoryChange != null
                       || ParameterChanges.Count > 0
                       || TypeChanges.Count > 0
                       || GeometryChanged;
            }
        }
    }
}
