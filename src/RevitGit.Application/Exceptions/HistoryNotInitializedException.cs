using System;
using RevitGit.Application.Models;

namespace RevitGit.Application.Exceptions
{
    public sealed class HistoryNotInitializedException : Exception
    {
        public HistoryNotInitializedException(FamilyIdentity familyIdentity)
            : base("History is not initialized for family: " + familyIdentity + ".")
        {
            FamilyIdentity = familyIdentity;
        }

        public FamilyIdentity FamilyIdentity { get; }
    }
}
