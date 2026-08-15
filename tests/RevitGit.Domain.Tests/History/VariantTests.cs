using System;
using System.Linq;
using RevitGit.Domain.Exceptions;
using RevitGit.Domain.History;
using Xunit;

namespace RevitGit.Domain.Tests.History
{
    public sealed class VariantTests
    {
        private static readonly DateTimeOffset InitialTime =
            new DateTimeOffset(2026, 8, 15, 10, 0, 0, TimeSpan.Zero);

        [Fact]
        public void CreateVariantFromOldVersionDoesNotMoveMainVariantOrSwitchCurrentVariant()
        {
            var history = FamilyHistory.Create("Main", InitialTime, null);
            var mainId = history.CurrentVariantId;
            var second = history.AddVersion(InitialTime.AddMinutes(1), null);
            var third = history.AddVersion(InitialTime.AddMinutes(2), null);

            var variant = history.CreateVariant(second.Id, "  Variant 1  ");

            Assert.Equal(second.Id, variant.CurrentVersionId);
            Assert.Equal("Variant 1", variant.Name);
            Assert.Equal(third.Id, history.GetVariant(mainId).CurrentVersionId);
            Assert.Equal(mainId, history.CurrentVariantId);
        }

        [Fact]
        public void SwitchedVariantContinuesIndependently()
        {
            var history = FamilyHistory.Create("Main", InitialTime, null);
            var mainId = history.CurrentVariantId;
            var second = history.AddVersion(InitialTime.AddMinutes(1), null);
            var third = history.AddVersion(InitialTime.AddMinutes(2), null);
            var variant = history.CreateVariant(second.Id, "Variant 1");

            history.SwitchVariant(variant.Id);
            var fourth = history.AddVersion(InitialTime.AddMinutes(3), null);

            Assert.Equal(second.Id, fourth.ParentVersionId);
            Assert.Equal(fourth.Id, history.GetVariant(variant.Id).CurrentVersionId);
            Assert.Equal(third.Id, history.GetVariant(mainId).CurrentVersionId);
        }

        [Fact]
        public void SwitchingVariantsDoesNotChangeVersions()
        {
            var history = FamilyHistory.Create("Main", InitialTime, null);
            var mainId = history.CurrentVariantId;
            var initial = history.GetVariant(mainId).CurrentVersionId;
            var variant = history.CreateVariant(initial, "Variant 1");
            var versionIdsBefore = history.Versions.Keys.ToArray();

            history.SwitchVariant(variant.Id);
            history.SwitchVariant(mainId);

            Assert.Equal(mainId, history.CurrentVariantId);
            Assert.Equal(versionIdsBefore, history.Versions.Keys.ToArray());
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void NewVariantNameCannotBeEmpty(string name)
        {
            var history = FamilyHistory.Create("Main", InitialTime, null);
            var initial = history.GetVariant(history.CurrentVariantId).CurrentVersionId;

            Assert.Throws<DomainException>(() => history.CreateVariant(initial, name));
        }
    }
}
