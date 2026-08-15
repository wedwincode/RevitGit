using System;
using Autodesk.Revit.DB;
using RevitGit.Application.Abstractions;
using RevitGit.Application.Models;
using RevitGit.Revit2021.Diagnostics;

namespace RevitGit.Revit2021.Documents
{
    public sealed class RevitFamilyDocumentGateway : IFamilyDocumentGateway
    {
        private readonly Document _document;
        private readonly SaveVersionTimings _timings;

        public RevitFamilyDocumentGateway(Document document, SaveVersionTimings timings)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
            _timings = timings ?? throw new ArgumentNullException(nameof(timings));
        }

        public FamilyIdentity GetIdentity()
        {
            return new FamilyIdentity(_document.PathName);
        }

        public void Save()
        {
            _timings.MeasureDocumentSave(() => _document.Save());
        }
    }
}
