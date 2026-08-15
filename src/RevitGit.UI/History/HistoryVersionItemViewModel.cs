using System;
using System.Globalization;
using RevitGit.Application.Models;
using RevitGit.Domain.Identifiers;

namespace RevitGit.UI.History
{
    public sealed class HistoryVersionItemViewModel
    {
        public HistoryVersionItemViewModel(VersionSummary summary)
        {
            if (summary == null)
            {
                throw new ArgumentNullException(nameof(summary));
            }

            Id = summary.Id;
            CreatedAtText = Format(summary.CreatedAt);
            Comment = summary.Comment;
            IsCurrent = summary.IsCurrent;
            CurrentLabel = summary.IsCurrent ? "Текущая" : null;
            IsRestored = summary.RestoredFromVersionId != null;
            RestoredFromText = summary.RestoredFromCreatedAt.HasValue
                ? "Восстановлено из версии от " + Format(summary.RestoredFromCreatedAt.Value)
                : IsRestored ? "Восстановленная версия" : null;
        }

        public VersionId Id { get; }
        public string CreatedAtText { get; }
        public string Comment { get; }
        public bool IsCurrent { get; }
        public string CurrentLabel { get; }
        public bool IsRestored { get; }
        public string RestoredFromText { get; }

        private static string Format(DateTimeOffset value)
        {
            return value.ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture);
        }
    }
}
