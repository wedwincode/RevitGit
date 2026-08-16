using System;
using System.Linq;
using RevitGit.Application.History;
using RevitGit.Application.Models;
using RevitGit.Application.Tests.Fakes;
using RevitGit.Domain.History;
using Xunit;

namespace RevitGit.Application.Tests.History
{
    public sealed class GetHistoryUseCaseTests
    {
        private static readonly DateTimeOffset InitialTime =
            new DateTimeOffset(2026, 8, 15, 10, 0, 0, TimeSpan.Zero);

        [Fact]
        public void ReturnsUiNeutralSummaryWithCurrentMarkersAndRestoreMetadata()
        {
            var identity = new FamilyIdentity("family-1");
            var history = FamilyHistory.Create("Main", InitialTime, "Initial");
            var mainId = history.CurrentVariantId;
            var first = history.GetVariant(mainId).CurrentVersionId;
            var second = history.AddVersion(InitialTime.AddMinutes(1), "Second");
            var third = history.AddVersion(InitialTime.AddMinutes(1), "Third");
            var variant = history.CreateVariant(second.Id, "Alternative");
            history.SwitchVariant(variant.Id);
            var restored = history.AddRestoredVersion(first, InitialTime.AddMinutes(2), "Restored");
            var repository = new FakeHistoryRepository();
            repository.Seed(identity, history);
            var useCase = new GetHistoryUseCase(repository, new FakeFamilyDocumentGateway(identity));

            var summary = useCase.Execute();

            Assert.Equal(variant.Id, summary.CurrentVariantId);
            Assert.Equal(2, summary.Variants.Count);
            Assert.True(summary.Variants.Single(item => item.Id.Equals(variant.Id)).IsCurrent);
            Assert.False(summary.Variants.Single(item => item.Id.Equals(mainId)).IsCurrent);
            Assert.Equal(third.Id, summary.Variants.Single(item => item.Id.Equals(mainId)).CurrentVersionId);
            Assert.Equal(new[] { restored.Id, second.Id, first }, summary.Versions.Select(item => item.Id));
            var restoredView = summary.Versions.Single(item => item.Id.Equals(restored.Id));
            Assert.Equal(first, restoredView.RestoredFromVersionId);
            Assert.Equal(InitialTime, restoredView.RestoredFromCreatedAt);
            Assert.Equal("Initial", restoredView.RestoredFromComment);
            Assert.Equal("Restored", restoredView.Comment);
            Assert.Equal(InitialTime.AddMinutes(2), restoredView.CreatedAt);
            Assert.True(restoredView.IsCurrent);
            Assert.Single(summary.Versions.Where(item => item.IsCurrent));
        }

        [Fact]
        public void VersionsUseCurrentVariantAncestryNewestFirst()
        {
            var identity = new FamilyIdentity("family-1");
            var history = FamilyHistory.Create("Main", InitialTime, null);
            var second = history.AddVersion(InitialTime.AddMinutes(1), "B");
            var mainTip = history.AddVersion(InitialTime.AddMinutes(2), "C");
            var alternative = history.CreateVariant(second.Id, "Alternative");
            history.SwitchVariant(alternative.Id);
            var alternativeTip = history.AddVersion(InitialTime.AddMinutes(3), "D");
            var repository = new FakeHistoryRepository();
            repository.Seed(identity, history);
            var useCase = new GetHistoryUseCase(repository, new FakeFamilyDocumentGateway(identity));
            var initial = history.Versions.Values.Single(version => version.ParentVersionId == null).Id;
            var expected = new[] { alternativeTip.Id, second.Id, initial };

            var firstRead = useCase.Execute().Versions.Select(version => version.Id).ToArray();
            var secondRead = useCase.Execute().Versions.Select(version => version.Id).ToArray();

            Assert.Equal(expected, firstRead);
            Assert.Equal(firstRead, secondRead);
            Assert.DoesNotContain(mainTip.Id, firstRead);
        }

        [Fact]
        public void MissingHistoryIsReportedWithoutCreatingIt()
        {
            var identity = new FamilyIdentity("family-1");
            var repository = new FakeHistoryRepository();
            var useCase = new GetHistoryUseCase(repository, new FakeFamilyDocumentGateway(identity));

            Assert.Throws<RevitGit.Application.Exceptions.HistoryNotInitializedException>(
                () => useCase.Execute());
            Assert.False(repository.Exists(identity));
        }
    }
}
