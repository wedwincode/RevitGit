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
            HasComment = !string.IsNullOrWhiteSpace(summary.Comment);
            PrimaryLabel = HasComment ? summary.Comment : CreatedAtText;
            SecondaryLabel = HasComment ? CreatedAtText : null;
            IsCurrent = summary.IsCurrent;
            CurrentLabel = summary.IsCurrent ? "Текущая" : null;
            IsRestored = summary.RestoredFromVersionId != null;
            RestoredFromText = FormatRestoreSource(
                IsRestored,
                summary.RestoredFromComment,
                summary.RestoredFromCreatedAt);
        }

        public VersionId Id { get; }
        public string CreatedAtText { get; }
        public string Comment { get; }
        public bool HasComment { get; }
        public string PrimaryLabel { get; }
        public string SecondaryLabel { get; }
        public bool IsCurrent { get; }
        public string CurrentLabel { get; }
        public bool IsRestored { get; }
        public bool HasRestoreProvenance => IsRestored;
        public string RestoredFromText { get; }

        private static string Format(DateTimeOffset value)
        {
            return value.ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture);
        }

        private static string FormatRestoreSource(
            bool isRestored,
            string sourceComment,
            DateTimeOffset? sourceCreatedAt)
        {
            if (!isRestored)
            {
                return null;
            }

            var hasComment = !string.IsNullOrWhiteSpace(sourceComment);
            if (hasComment && sourceCreatedAt.HasValue)
            {
                return "Восстановленная версия\nИсточник: " + sourceComment + " — " + Format(sourceCreatedAt.Value);
            }

            if (hasComment)
            {
                return "Восстановленная версия\nИсточник: " + sourceComment;
            }

            return sourceCreatedAt.HasValue
                ? "Восстановленная версия\nИсточник: " + Format(sourceCreatedAt.Value)
                : "Восстановленная версия";
        }
    }
}
