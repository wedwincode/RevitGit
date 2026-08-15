using System;
using RevitGit.Domain.History;
using Xunit;

namespace RevitGit.Domain.Tests.History
{
    public sealed class RestoreTests
    {
        private static readonly DateTimeOffset InitialTime =
            new DateTimeOffset(2026, 8, 15, 10, 0, 0, TimeSpan.Zero);

        [Fact]
        public void RestoreOldVersionAddsNewVersionWithoutRewritingHistory()
        {
            var history = FamilyHistory.Create("Main", InitialTime, null);
            var first = history.GetVariant(history.CurrentVariantId).CurrentVersionId;
            var second = history.AddVersion(InitialTime.AddMinutes(1), null);
            var third = history.AddVersion(InitialTime.AddMinutes(2), null);

            var restored = history.AddRestoredVersion(
                first,
                InitialTime.AddMinutes(3),
                "Restored first version");

            Assert.Equal(third.Id, restored.ParentVersionId);
            Assert.Equal(first, restored.RestoredFromVersionId);
            Assert.Equal(restored.Id, history.GetVariant(history.CurrentVariantId).CurrentVersionId);
            Assert.Equal(4, history.Versions.Count);
            Assert.Same(second, history.GetVersion(second.Id));
            Assert.Same(third, history.GetVersion(third.Id));
        }

        [Fact]
        public void RestoreInsideSecondaryVariantOnlyAdvancesThatVariant()
        {
            var history = FamilyHistory.Create("Main", InitialTime, null);
            var mainId = history.CurrentVariantId;
            var first = history.GetVariant(mainId).CurrentVersionId;
            var second = history.AddVersion(InitialTime.AddMinutes(1), null);
            var third = history.AddVersion(InitialTime.AddMinutes(2), null);
            var variant = history.CreateVariant(second.Id, "Variant 1");
            history.SwitchVariant(variant.Id);
            var fourth = history.AddVersion(InitialTime.AddMinutes(3), null);

            var restored = history.AddRestoredVersion(
                first,
                InitialTime.AddMinutes(4),
                null);

            Assert.Equal(fourth.Id, restored.ParentVersionId);
            Assert.Equal(first, restored.RestoredFromVersionId);
            Assert.Equal(restored.Id, history.GetVariant(variant.Id).CurrentVersionId);
            Assert.Equal(third.Id, history.GetVariant(mainId).CurrentVersionId);
        }
    }
}
