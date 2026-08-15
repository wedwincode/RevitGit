using RevitGit.Domain;
using Xunit;

namespace RevitGit.Domain.Tests
{
    public sealed class BootstrapSmokeTest
    {
        [Fact]
        public void DomainReferenceIsAvailable()
        {
            var domainAssembly = typeof(AssemblyMarker).Assembly;

            Assert.Equal("RevitGit.Domain", domainAssembly.GetName().Name);
        }
    }
}
