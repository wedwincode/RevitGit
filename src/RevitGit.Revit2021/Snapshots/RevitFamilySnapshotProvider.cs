using System;
using Autodesk.Revit.DB;
using RevitGit.Application.Abstractions;
using RevitGit.Domain.Snapshots;
using RevitGit.Revit2021.Diagnostics;

namespace RevitGit.Revit2021.Snapshots
{
    public sealed class RevitFamilySnapshotProvider : IFamilySnapshotProvider
    {
        private readonly Document _document;
        private readonly RevitFamilySnapshotExtractor _extractor;
        private readonly SaveVersionTimings _timings;

        public RevitFamilySnapshotProvider(
            Document document,
            RevitFamilySnapshotExtractor extractor,
            SaveVersionTimings timings)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
            _extractor = extractor ?? throw new ArgumentNullException(nameof(extractor));
            _timings = timings ?? throw new ArgumentNullException(nameof(timings));
        }

        public FamilySnapshot CaptureSnapshot()
        {
            return _timings.MeasureSnapshot(() => _extractor.Extract(_document));
        }
    }
}
