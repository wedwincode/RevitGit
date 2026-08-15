using System;
using System.Diagnostics;
using Autodesk.Revit.DB;
using RevitGit.Application.Abstractions;
using RevitGit.Application.History;
using RevitGit.Application.Compare;
using RevitGit.Application.Diff;
using RevitGit.Application.Models;
using RevitGit.Infrastructure.FileSystem.Repositories;
using RevitGit.Infrastructure.FileSystem.Serialization;
using RevitGit.Revit2021.Commands;
using RevitGit.Revit2021.Diagnostics;
using RevitGit.Revit2021.Documents;
using RevitGit.Revit2021.Snapshots;
using RevitGit.Domain.Identifiers;
using RevitGit.Domain.Snapshots;

namespace RevitGit.Revit2021.Composition
{
    public static class RevitCompositionRoot
    {
        public static ISaveVersionOperation CreateSaveVersionOperation(
            Document document,
            SaveVersionTimings timings)
        {
            var clock = new SystemClock();
            var gateway = new RevitFamilyDocumentGateway(document, timings);
            var snapshotProvider = new RevitFamilySnapshotProvider(
                document,
                new RevitFamilySnapshotExtractor(),
                timings);
            var store = new LazyFamilyHistoryStore(
                document.PathName,
                new FamilyRepositoryManager(clock),
                snapshotProvider,
                new SnapshotJsonSerializer());
            var useCase = new SaveVersionUseCase(store, gateway, store, clock);
            return new RevitSaveVersionOperation(useCase, store, gateway, timings);
        }

        public static GetHistoryUseCase CreateGetHistoryUseCase(Document document)
        {
            var timings = new SaveVersionTimings();
            var clock = new SystemClock();
            var gateway = new RevitFamilyDocumentGateway(document, timings);
            var snapshotProvider = new RevitFamilySnapshotProvider(
                document,
                new RevitFamilySnapshotExtractor(),
                timings);
            var store = new LazyFamilyHistoryStore(
                document.PathName,
                new FamilyRepositoryManager(clock),
                snapshotProvider,
                new SnapshotJsonSerializer());
            return new GetHistoryUseCase(store, gateway);
        }

        public static FamilyDiff CompareSavedVersions(string familyPath, VersionId before, VersionId after)
        {
            var store = CreateReadStore(familyPath);
            return new CompareSavedVersionsUseCase(store, store, new FamilyDiffEngine())
                .Execute(new FamilyIdentity(familyPath), before, after);
        }

        public static FamilyDiff CompareVersionWithSnapshot(string familyPath, VersionId before, FamilySnapshot current)
        {
            var store = CreateReadStore(familyPath);
            return new CompareVersionWithSnapshotUseCase(store, store, new FamilyDiffEngine())
                .Execute(new FamilyIdentity(familyPath), before, current);
        }

        public static RestoreVersionUseCase CreateRestoreVersionUseCase(Document document)
        {
            var timings = new SaveVersionTimings();
            var clock = new SystemClock();
            var gateway = new RevitFamilyDocumentGateway(document, timings);
            var store = new LazyFamilyHistoryStore(document.PathName, new FamilyRepositoryManager(clock),
                new RevitFamilySnapshotProvider(document, new RevitFamilySnapshotExtractor(), timings),
                new SnapshotJsonSerializer());
            return new RestoreVersionUseCase(store, gateway, store, clock);
        }

        private static LazyFamilyHistoryStore CreateReadStore(string familyPath)
        {
            var clock = new SystemClock();
            return new LazyFamilyHistoryStore(familyPath, new FamilyRepositoryManager(clock),
                new UnavailableSnapshotProvider(), new SnapshotJsonSerializer());
        }

        private sealed class UnavailableSnapshotProvider : IFamilySnapshotProvider
        {
            public FamilySnapshot CaptureSnapshot() => throw new InvalidOperationException("Read-only history access cannot capture a snapshot.");
        }

        private sealed class RevitSaveVersionOperation : ISaveVersionOperation
        {
            private readonly SaveVersionUseCase _useCase;
            private readonly LazyFamilyHistoryStore _store;
            private readonly IFamilyDocumentGateway _gateway;
            private readonly SaveVersionTimings _timings;

            public RevitSaveVersionOperation(
                SaveVersionUseCase useCase,
                LazyFamilyHistoryStore store,
                IFamilyDocumentGateway gateway,
                SaveVersionTimings timings)
            {
                _useCase = useCase;
                _store = store;
                _gateway = gateway;
                _timings = timings;
            }

            public VersionSummary Execute(string comment)
            {
                var stopwatch = Stopwatch.StartNew();
                try
                {
                    var version = _useCase.Execute(comment, "Основной");
                    _store.Validate(_gateway.GetIdentity());
                    return version;
                }
                finally
                {
                    stopwatch.Stop();
                    _timings.Complete(stopwatch.ElapsedMilliseconds);
                    _timings.WriteToDebug();
                }
            }
        }

        private sealed class SystemClock : IClock
        {
            public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
        }
    }
}
