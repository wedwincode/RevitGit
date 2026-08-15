using System.Collections.Generic;

namespace RevitGit.UI.Compare
{
    public sealed class ParameterChangeViewModel
    {
        public ParameterChangeViewModel(string name, string changeLabel, IReadOnlyList<CompareFieldViewModel> fields)
        { Name = name; ChangeLabel = changeLabel; Fields = fields; }
        public string Name { get; }
        public string ChangeLabel { get; }
        public IReadOnlyList<CompareFieldViewModel> Fields { get; }
    }
}
