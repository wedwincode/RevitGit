namespace RevitGit.UI.Compare
{
    public sealed class ParameterValueChangeViewModel
    {
        public ParameterValueChangeViewModel(string name, string changeLabel, string before, string after)
        { Name = name; ChangeLabel = changeLabel; Before = before; After = after; }
        public string Name { get; }
        public string ChangeLabel { get; }
        public string Before { get; }
        public string After { get; }
    }
}
