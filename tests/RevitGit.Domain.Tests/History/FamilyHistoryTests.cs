using System;
using RevitGit.Domain.Exceptions;
using RevitGit.Domain.History;
using Xunit;

namespace RevitGit.Domain.Tests.History
{
    public sealed class FamilyHistoryTests
    {
        private static readonly DateTimeOffset InitialTime =
            new DateTimeOffset(2026, 8, 15, 10, 0, 0, TimeSpan.Zero);

        [Fact]
        public void CreateBuildsMainVariantAtInitialVersion()
        {
            var history = FamilyHistory.Create("Main", InitialTime, "Initial version");

            Assert.Single(history.Versions);
            Assert.Single(history.Variants);

            var main = history.GetVariant(history.CurrentVariantId);
            var initial = history.GetVersion(main.CurrentVersionId);

            Assert.Equal("Main", main.Name);
            Assert.Null(initial.ParentVersionId);
            Assert.Null(initial.RestoredFromVersionId);
            Assert.Equal(InitialTime, initial.CreatedAt);
            Assert.Equal("Initial version", initial.Comment);
        }

        [Fact]
        public void AddVersionUsesCurrentVersionAsParentAndMovesCurrentVariant()
        {
            var history = FamilyHistory.Create("Main", InitialTime, null);
            var initial = history.GetVariant(history.CurrentVariantId).CurrentVersionId;

            var added = history.AddVersion(InitialTime.AddMinutes(1), "Changed width");

            Assert.Equal(initial, added.ParentVersionId);
            Assert.Equal(added.Id, history.GetVariant(history.CurrentVariantId).CurrentVersionId);
        }

        [Fact]
        public void AddingVersionsBuildsLinearParentChain()
        {
            var history = FamilyHistory.Create("Main", InitialTime, null);
            var first = history.GetVariant(history.CurrentVariantId).CurrentVersionId;
            var second = history.AddVersion(InitialTime.AddMinutes(1), null);
            var third = history.AddVersion(InitialTime.AddMinutes(2), null);

            Assert.Equal(first, second.ParentVersionId);
            Assert.Equal(second.Id, third.ParentVersionId);
            Assert.Equal(third.Id, history.GetVariant(history.CurrentVariantId).CurrentVersionId);
            Assert.Equal(3, history.Versions.Count);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void MissingCommentIsNormalizedToNull(string comment)
        {
            var history = FamilyHistory.Create("Main", InitialTime, comment);

            var version = history.GetVariant(history.CurrentVariantId).CurrentVersionId;

            Assert.Null(history.GetVersion(version).Comment);
        }

        [Fact]
        public void CommentIsTrimmed()
        {
            var history = FamilyHistory.Create("Main", InitialTime, "  Changed width  ");

            var version = history.GetVariant(history.CurrentVariantId).CurrentVersionId;

            Assert.Equal("Changed width", history.GetVersion(version).Comment);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void MainVariantNameCannotBeEmpty(string name)
        {
            Assert.Throws<DomainException>(() => FamilyHistory.Create(name, InitialTime, null));
        }
    }
}
