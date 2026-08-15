using System;
using System.Collections.Generic;
using RevitGit.Domain.Exceptions;
using RevitGit.Domain.History;
using RevitGit.Domain.Identifiers;
using Xunit;
using HistoryVersion = RevitGit.Domain.History.Version;

namespace RevitGit.Domain.Tests.History
{
    public sealed class InvariantTests
    {
        private static readonly DateTimeOffset InitialTime =
            new DateTimeOffset(2026, 8, 15, 10, 0, 0, TimeSpan.Zero);

        [Fact]
        public void CreateVariantFromUnknownVersionThrowsDomainError()
        {
            var history = FamilyHistory.Create("Main", InitialTime, null);
            var unknown = new VersionId(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));

            Assert.Throws<UnknownVersionException>(
                () => history.CreateVariant(unknown, "Variant 1"));
        }

        [Fact]
        public void RestoreUnknownVersionThrowsDomainErrorWithoutChangingHistory()
        {
            var history = FamilyHistory.Create("Main", InitialTime, null);
            var current = history.GetVariant(history.CurrentVariantId).CurrentVersionId;
            var unknown = new VersionId(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"));

            Assert.Throws<UnknownVersionException>(
                () => history.AddRestoredVersion(unknown, InitialTime.AddMinutes(1), null));

            Assert.Single(history.Versions);
            Assert.Equal(current, history.GetVariant(history.CurrentVariantId).CurrentVersionId);
        }

        [Fact]
        public void SwitchToUnknownVariantThrowsDomainErrorWithoutChangingHistory()
        {
            var history = FamilyHistory.Create("Main", InitialTime, null);
            var currentVariant = history.CurrentVariantId;
            var unknown = new VariantId(Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"));

            Assert.Throws<UnknownVariantException>(() => history.SwitchVariant(unknown));

            Assert.Equal(currentVariant, history.CurrentVariantId);
            Assert.Single(history.Versions);
        }

        [Fact]
        public void PublishedCollectionsCannotBeMutatedExternally()
        {
            var history = FamilyHistory.Create("Main", InitialTime, null);

            var versions = (IDictionary<VersionId, HistoryVersion>)history.Versions;
            var variants = (IDictionary<VariantId, Variant>)history.Variants;

            Assert.Throws<NotSupportedException>(() => versions.Clear());
            Assert.Throws<NotSupportedException>(() => variants.Clear());
            Assert.Single(history.Versions);
            Assert.Single(history.Variants);
        }
    }
}
