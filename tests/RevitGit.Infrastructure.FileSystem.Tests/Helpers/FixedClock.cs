using System;
using RevitGit.Application.Abstractions;

namespace RevitGit.Infrastructure.FileSystem.Tests.Helpers
{
    internal sealed class FixedClock : IClock
    {
        public FixedClock(DateTimeOffset utcNow)
        {
            UtcNow = utcNow;
        }

        public DateTimeOffset UtcNow { get; }
    }
}
