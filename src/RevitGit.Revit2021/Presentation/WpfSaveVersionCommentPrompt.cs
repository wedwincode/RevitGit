using System;
using System.Windows.Interop;
using RevitGit.Revit2021.Commands;
using RevitGit.UI.SaveVersion;

namespace RevitGit.Revit2021.Presentation
{
    internal sealed class WpfSaveVersionCommentPrompt
    {
        private readonly IntPtr _owner;

        public WpfSaveVersionCommentPrompt(IntPtr owner)
        {
            _owner = owner;
        }

        public SaveVersionCommentPromptResult Show()
        {
            var dialog = new SaveVersionDialog();
            if (_owner != IntPtr.Zero)
            {
                new WindowInteropHelper(dialog).Owner = _owner;
            }

            var confirmed = dialog.ShowDialog() == true;
            return new SaveVersionCommentPromptResult(confirmed, dialog.Comment);
        }
    }
}
