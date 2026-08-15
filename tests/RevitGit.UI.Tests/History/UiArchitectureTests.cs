using System.Linq;
using RevitGit.UI.History;
using RevitGit.Application.History;
using RevitGit.Domain.History;
using Xunit;

namespace RevitGit.UI.Tests.History
{
    public sealed class UiArchitectureTests
    {
        [Fact]
        public void UiAssemblyDoesNotReferenceAutodeskOrLibGit2Sharp()
        {
            var references = typeof(HistoryViewModel).Assembly.GetReferencedAssemblies();

            Assert.DoesNotContain(references, item => item.Name.StartsWith("Autodesk.Revit"));
            Assert.DoesNotContain(references, item => item.Name == "LibGit2Sharp");
        }

        [Fact]
        public void CoreAssembliesDoNotReferenceAutodeskRevit()
        {
            var assemblies = new[] { typeof(FamilyHistory).Assembly, typeof(GetHistoryUseCase).Assembly };

            Assert.All(
                assemblies,
                assembly => Assert.DoesNotContain(
                    assembly.GetReferencedAssemblies(),
                    reference => reference.Name.StartsWith("Autodesk.Revit")));
        }
    }
}
