using System;
using System.Linq;
using RevitGit.Domain.Exceptions;
using RevitGit.Domain.History;
using RevitGit.Domain.Identifiers;
using Xunit;
using HistoryVersion = RevitGit.Domain.History.Version;

namespace RevitGit.Domain.Tests.History
{
    public sealed class RehydrationTests
    {
        private static readonly DateTimeOffset InitialTime =
            new DateTimeOffset(2026, 8, 15, 10, 0, 0, TimeSpan.Zero);

        [Fact]
        public void RehydratePreservesIdsGraphMetadataAndCurrentVariant()
        {
            var firstId = VersionId("11111111-1111-1111-1111-111111111111");
            var secondId = VersionId("22222222-2222-2222-2222-222222222222");
            var restoredId = VersionId("33333333-3333-3333-3333-333333333333");
            var mainId = VariantId("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
            var alternativeId = VariantId("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
            var versions = new[]
            {
                HistoryVersion.Rehydrate(firstId, null, InitialTime, "Initial", null),
                HistoryVersion.Rehydrate(secondId, firstId, InitialTime.AddMinutes(1), "Second", null),
                HistoryVersion.Rehydrate(restoredId, secondId, InitialTime.AddMinutes(2), "Restore", firstId)
            };
            var variants = new[]
            {
                Variant.Rehydrate(mainId, "Main", secondId),
                Variant.Rehydrate(alternativeId, "Alternative", restoredId)
            };

            var history = FamilyHistory.Rehydrate(versions, variants, alternativeId);

            Assert.Equal(alternativeId, history.CurrentVariantId);
            Assert.Equal(3, history.Versions.Count);
            Assert.Equal(2, history.Variants.Count);
            Assert.Equal(firstId, history.GetVersion(restoredId).RestoredFromVersionId);
            Assert.Equal(secondId, history.GetVersion(restoredId).ParentVersionId);
            Assert.Equal("Restore", history.GetVersion(restoredId).Comment);
            Assert.Equal(restoredId, history.GetVariant(alternativeId).CurrentVersionId);
        }

        [Fact]
        public void RehydrateRejectsDanglingReferences()
        {
            var firstId = VersionId("11111111-1111-1111-1111-111111111111");
            var missingId = VersionId("99999999-9999-9999-9999-999999999999");
            var mainId = VariantId("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

            Assert.Throws<DomainException>(() => FamilyHistory.Rehydrate(
                new[] { HistoryVersion.Rehydrate(firstId, missingId, InitialTime, null, null) },
                new[] { Variant.Rehydrate(mainId, "Main", firstId) },
                mainId));
        }

        [Fact]
        public void AddVersionUsesProvidedIdAndRejectsDuplicateWithoutMutation()
        {
            var history = FamilyHistory.Create("Main", InitialTime, null);
            var id = VersionId("44444444-4444-4444-4444-444444444444");

            var added = history.AddVersion(id, InitialTime.AddMinutes(1), "Saved");
            var count = history.Versions.Count;

            Assert.Equal(id, added.Id);
            Assert.Throws<DomainException>(
                () => history.AddVersion(id, InitialTime.AddMinutes(2), "Duplicate"));
            Assert.Equal(count, history.Versions.Count);
            Assert.Equal(id, history.GetVariant(history.CurrentVariantId).CurrentVersionId);
        }

        private static VersionId VersionId(string value)
        {
            return new VersionId(Guid.Parse(value));
        }

        private static VariantId VariantId(string value)
        {
            return new VariantId(Guid.Parse(value));
        }
    }
}
