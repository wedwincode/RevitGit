using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using RevitGit.Application.Abstractions;
using RevitGit.Application.Exceptions;
using RevitGit.Application.Models;

namespace RevitGit.Application.History
{
    public sealed class GetHistoryUseCase
    {
        private readonly IHistoryRepository _historyRepository;
        private readonly IFamilyDocumentGateway _documentGateway;

        public GetHistoryUseCase(
            IHistoryRepository historyRepository,
            IFamilyDocumentGateway documentGateway)
        {
            _historyRepository = historyRepository;
            _documentGateway = documentGateway;
        }

        public HistorySummary Execute()
        {
            var familyIdentity = _documentGateway.GetIdentity();
            var history = _historyRepository.LoadRequired(familyIdentity);
            var currentVersionId = history.GetVariant(history.CurrentVariantId).CurrentVersionId;

            var variants = history.Variants.Values
                .OrderBy(variant => variant.Name, StringComparer.Ordinal)
                .ThenBy(variant => variant.Id.Value)
                .Select(variant => new VariantSummary(
                    variant.Id,
                    variant.Name,
                    variant.CurrentVersionId,
                    variant.Id.Equals(history.CurrentVariantId)))
                .ToList();

            var versions = GetCurrentVariantAncestry(history, currentVersionId)
                .Select(version => new VersionSummary(
                    version.Id,
                    version.ParentVersionId,
                    version.CreatedAt,
                    version.Comment,
                    version.RestoredFromVersionId,
                    version.Id.Equals(currentVersionId),
                    version.RestoredFromVersionId == null
                        ? (DateTimeOffset?)null
                        : history.GetVersion(version.RestoredFromVersionId).CreatedAt))
                .ToList();

            return new HistorySummary(
                history.CurrentVariantId,
                new ReadOnlyCollection<VariantSummary>(variants),
                new ReadOnlyCollection<VersionSummary>(versions));
        }

        private static IEnumerable<Domain.History.Version> GetCurrentVariantAncestry(
            Domain.History.FamilyHistory history,
            Domain.Identifiers.VersionId currentVersionId)
        {
            var visited = new HashSet<Domain.Identifiers.VersionId>();
            var versionId = currentVersionId;
            while (versionId != null)
            {
                if (!visited.Add(versionId))
                {
                    throw new InvalidOperationException("History ancestry contains a cycle.");
                }

                var version = history.GetVersion(versionId);
                yield return version;
                versionId = version.ParentVersionId;
            }
        }

    }
}
