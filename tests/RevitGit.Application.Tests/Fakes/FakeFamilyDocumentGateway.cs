using System;
using System.Collections.Generic;
using RevitGit.Application.Abstractions;
using RevitGit.Application.Models;

namespace RevitGit.Application.Tests.Fakes
{
    internal sealed class FakeFamilyDocumentGateway : IFamilyDocumentGateway
    {
        private readonly IList<string> _operations;

        public FakeFamilyDocumentGateway(FamilyIdentity identity, IList<string> operations = null)
        {
            Identity = identity;
            _operations = operations;
        }

        public FamilyIdentity Identity { get; }

        public bool SaveCalled { get; private set; }

        public Exception SaveException { get; set; }

        public FamilyIdentity GetIdentity()
        {
            return Identity;
        }

        public void Save()
        {
            SaveCalled = true;
            _operations?.Add("document-save");
            if (SaveException != null)
            {
                throw SaveException;
            }
        }
    }
}
