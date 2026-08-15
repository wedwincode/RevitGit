using System.Collections.Generic;

namespace RevitGit.UI.Compare
{
    public sealed class FamilyTypeChangeViewModel
    {
        public FamilyTypeChangeViewModel(string name, string changeLabel, IReadOnlyList<ParameterValueChangeViewModel> values)
        { Name = name; ChangeLabel = changeLabel; Values = values; }
        public string Name { get; }
        public string ChangeLabel { get; }
        public IReadOnlyList<ParameterValueChangeViewModel> Values { get; }
    }
}
