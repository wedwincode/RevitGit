using System;
using RevitGit.Application.Abstractions;
using RevitGit.Application.Exceptions;
using RevitGit.Application.Models;
using RevitGit.Domain.History;

namespace RevitGit.Application.History
{
    internal static class HistoryRepositoryExtensions
    {
        public static FamilyHistory LoadRequired(
            this IHistoryRepository historyRepository,
            FamilyIdentity familyIdentity)
        {
            if (!historyRepository.Exists(familyIdentity))
            {
                throw new HistoryNotInitializedException(familyIdentity);
            }

            var history = historyRepository.Load(familyIdentity);
            if (history == null)
            {
                throw new HistoryNotInitializedException(familyIdentity);
            }

            return history;
        }

        public static void SaveUpdated(
            this IHistoryRepository historyRepository,
            FamilyIdentity familyIdentity,
            FamilyHistory history)
        {
            try
            {
                historyRepository.Save(familyIdentity, history);
            }
            catch (Exception exception)
            {
                throw new ApplicationOperationException(
                    ApplicationFailureStage.PersistHistory,
                    "The updated history could not be persisted.",
                    exception);
            }
        }
    }
}
