using System;

namespace RevitGit.Application.Abstractions
{
    public interface IClock
    {
        DateTimeOffset UtcNow { get; }
    }
}
