using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using RevitGit.Domain.Snapshots;
using ParameterValue = RevitGit.Domain.Snapshots.ParameterValue;

namespace RevitGit.Revit2021.Snapshots
{
    public sealed class RevitFamilySnapshotExtractor
    {
        private readonly RevitGeometryFingerprintBuilder _geometryFingerprintBuilder =
            new RevitGeometryFingerprintBuilder();

        public FamilySnapshot Extract(Document document)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            if (!document.IsFamilyDocument)
            {
                throw new NotFamilyDocumentException();
            }

            try
            {
                var ownerFamily = document.OwnerFamily;
                if (ownerFamily == null || string.IsNullOrWhiteSpace(ownerFamily.Name))
                {
                    throw new SnapshotExtractionException("The family name is unavailable from Document.OwnerFamily.");
                }

                var category = ownerFamily.FamilyCategory;
                if (category == null || string.IsNullOrWhiteSpace(category.Name))
                {
                    throw new SnapshotExtractionException("The family category is unavailable from Document.OwnerFamily.");
                }

                var manager = document.FamilyManager;
                var parameters = ExtractParameters(manager);
                var types = ExtractTypes(document, manager, parameters);
                var parameterSnapshots = new List<FamilyParameterSnapshot>();
                foreach (var parameter in parameters)
                {
                    parameterSnapshots.Add(parameter.Snapshot);
                }

                return new FamilySnapshot(
                    ownerFamily.Name,
                    category.Name,
                    parameterSnapshots,
                    types,
                    _geometryFingerprintBuilder.Build(document));
            }
            catch (SnapshotExtractionException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new SnapshotExtractionException(
                    "Revit could not extract a deterministic family snapshot.",
                    exception);
            }
        }

        private static IList<ParameterEntry> ExtractParameters(FamilyManager manager)
        {
            var result = new List<ParameterEntry>();
            foreach (var parameter in manager.GetParameters())
            {
                var definition = parameter.Definition;
                var dataType = RevitParameterDataTypeMapper.Map(definition.ParameterType);
                var snapshot = RevitParameterValuePolicy.CreateParameterSnapshot(
                    RevitParameterStableKeyBuilder.Build(parameter),
                    definition.Name,
                    dataType,
                    parameter.IsInstance ? ParameterScope.Instance : ParameterScope.Type,
                    parameter.Formula);
                result.Add(new ParameterEntry(parameter, snapshot));
            }

            return result;
        }

        private static IList<FamilyTypeSnapshot> ExtractTypes(
            Document document,
            FamilyManager manager,
            IEnumerable<ParameterEntry> parameters)
        {
            var result = new List<FamilyTypeSnapshot>();
            foreach (FamilyType familyType in manager.Types)
            {
                var values = new List<KeyValuePair<string, ParameterValue>>();
                foreach (var parameter in parameters)
                {
                    ParameterValue value;
                    if (RevitParameterValueExtractor.TryExtract(
                        document,
                        familyType,
                        parameter.RevitParameter,
                        parameter.Snapshot.DataType,
                        out value))
                    {
                        values.Add(new KeyValuePair<string, ParameterValue>(parameter.Snapshot.StableKey, value));
                    }
                }

                result.Add(new FamilyTypeSnapshot(familyType.Name, values));
            }

            return result;
        }

        private sealed class ParameterEntry
        {
            public ParameterEntry(FamilyParameter revitParameter, FamilyParameterSnapshot snapshot)
            {
                RevitParameter = revitParameter;
                Snapshot = snapshot;
            }

            public FamilyParameter RevitParameter { get; }

            public FamilyParameterSnapshot Snapshot { get; }
        }
    }
}
