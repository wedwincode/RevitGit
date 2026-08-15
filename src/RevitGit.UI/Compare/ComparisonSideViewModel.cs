namespace RevitGit.UI.Compare
{
    public sealed class ComparisonSideViewModel
    {
        public ComparisonSideViewModel(string label, string comment) { Label = label; Comment = comment; }
        public string Label { get; }
        public string Comment { get; }
    }
}
