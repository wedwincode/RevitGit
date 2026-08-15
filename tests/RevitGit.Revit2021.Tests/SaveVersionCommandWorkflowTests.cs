using RevitGit.Revit2021.Commands;
using Xunit;

namespace RevitGit.Revit2021.Tests
{
    public sealed class SaveVersionCommandWorkflowTests
    {
        [Fact]
        public void CancelDoesNotExecuteSaveVersionOperation()
        {
            var operation = new RecordingSaveVersionOperation();
            var workflow = new SaveVersionCommandWorkflow();

            var result = workflow.Execute(
                new SaveVersionCommentPromptResult(false, "Не должно сохраниться"),
                operation);

            Assert.True(result.Cancelled);
            Assert.False(operation.ExecuteCalled);
        }

        private sealed class RecordingSaveVersionOperation : ISaveVersionOperation
        {
            public bool ExecuteCalled { get; private set; }

            public RevitGit.Application.Models.VersionSummary Execute(string comment)
            {
                ExecuteCalled = true;
                return null;
            }
        }
    }
}
