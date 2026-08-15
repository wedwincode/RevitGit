using System;
using RevitGit.Application.Models;

namespace RevitGit.Revit2021.Commands
{
    public sealed class SaveVersionCommandWorkflow
    {
        public SaveVersionWorkflowResult Execute(
            SaveVersionCommentPromptResult promptResult,
            ISaveVersionOperation operation)
        {
            if (promptResult == null)
            {
                throw new ArgumentNullException(nameof(promptResult));
            }

            if (operation == null)
            {
                throw new ArgumentNullException(nameof(operation));
            }

            if (!promptResult.Confirmed)
            {
                return SaveVersionWorkflowResult.CancelledResult();
            }

            return SaveVersionWorkflowResult.Succeeded(operation.Execute(promptResult.Comment));
        }
    }

    public sealed class SaveVersionWorkflowResult
    {
        private SaveVersionWorkflowResult(bool cancelled, VersionSummary version)
        {
            Cancelled = cancelled;
            Version = version;
        }

        public bool Cancelled { get; }

        public VersionSummary Version { get; }

        public static SaveVersionWorkflowResult CancelledResult()
        {
            return new SaveVersionWorkflowResult(true, null);
        }

        public static SaveVersionWorkflowResult Succeeded(VersionSummary version)
        {
            return new SaveVersionWorkflowResult(false, version ?? throw new ArgumentNullException(nameof(version)));
        }
    }
}
