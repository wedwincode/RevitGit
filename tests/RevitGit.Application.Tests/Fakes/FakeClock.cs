using System;
using RevitGit.Application.Abstractions;

namespace RevitGit.Application.Tests.Fakes
{
    internal sealed class FakeClock : IClock
    {
        public FakeClock(DateTimeOffset utcNow)
        {
            UtcNow = utcNow;
        }

        public DateTimeOffset UtcNow { get; set; }
    }
}
