using System;

namespace RevitGit.Application.Exceptions
{
    public sealed class ApplicationOperationException : Exception
    {
        public ApplicationOperationException(
            ApplicationFailureStage stage,
            string message,
            Exception innerException)
            : base(message, innerException)
        {
            Stage = stage;
        }

        public ApplicationFailureStage Stage { get; }
    }
}
