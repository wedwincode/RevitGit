using System.Collections.Generic;
using System.Linq;
using RevitGit.Domain.Snapshots;
using RevitGit.Infrastructure.FileSystem.Serialization;
using Xunit;

namespace RevitGit.Infrastructure.FileSystem.Tests.Serialization
{
    public sealed class SnapshotJsonSerializerTests
    {
        [Fact]
        public void SnapshotRoundTripPreservesSemanticEquality()
        {
            var snapshot = Snapshot();
            var serializer = new SnapshotJsonSerializer();

            var restored = serializer.Deserialize(serializer.Serialize(snapshot));

            Assert.Equal(snapshot, restored);
            Assert.NotSame(snapshot, restored);
        }

        [Fact]
        public void SerializationIsDeterministicForLogicalSnapshotOrder()
        {
            var first = Snapshot();
            var second = new FamilySnapshot(
                "Door",
                "Doors",
                first.Parameters.Reverse(),
                first.Types.Reverse(),
                "geometry-a");
            var serializer = new SnapshotJsonSerializer();

            Assert.Equal(serializer.Serialize(first), serializer.Serialize(second));
        }

        internal static FamilySnapshot Snapshot()
        {
            var parameters = new[]
            {
                new FamilyParameterSnapshot(
                    "family:width", "Width", ParameterDataType.Length, ParameterScope.Type, null),
                new FamilyParameterSnapshot(
                    "family:label", "Label", ParameterDataType.Text, ParameterScope.Instance, " Width / 2 "),
                new FamilyParameterSnapshot(
                    "family:count", "Count", ParameterDataType.Integer, ParameterScope.Type, null),
                new FamilyParameterSnapshot(
                    "family:enabled", "Enabled", ParameterDataType.YesNo, ParameterScope.Type, null),
                new FamilyParameterSnapshot(
                    "family:optional", "Optional", ParameterDataType.Unknown, ParameterScope.Type, null)
            };
            var values = new Dictionary<string, ParameterValue>
            {
                { "family:width", ParameterValue.FromDouble(900.00000001) },
                { "family:label", ParameterValue.FromString("Oak") },
                { "family:count", ParameterValue.FromInteger(2) },
                { "family:enabled", ParameterValue.FromBoolean(true) },
                { "family:optional", ParameterValue.Null }
            };

            return new FamilySnapshot(
                "Door",
                "Doors",
                parameters,
                new[]
                {
                    new FamilyTypeSnapshot("Wide", values),
                    new FamilyTypeSnapshot("Default", values)
                },
                "geometry-a");
        }
    }
}
