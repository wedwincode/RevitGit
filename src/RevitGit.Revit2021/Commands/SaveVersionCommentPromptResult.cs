namespace RevitGit.Revit2021.Commands
{
    public sealed class SaveVersionCommentPromptResult
    {
        public SaveVersionCommentPromptResult(bool confirmed, string comment)
        {
            Confirmed = confirmed;
            Comment = comment;
        }

        public bool Confirmed { get; }

        public string Comment { get; }
    }
}
