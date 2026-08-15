namespace RevitGit.UI.Compare
{
    public sealed class CompareFieldViewModel
    {
        public CompareFieldViewModel(string label, string before, string after)
        { Label = label; Before = before; After = after; }
        public string Label { get; }
        public string Before { get; }
        public string After { get; }
    }
}
