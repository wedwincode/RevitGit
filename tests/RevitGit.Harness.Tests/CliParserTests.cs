using System.Linq;
using System.IO;
using Xunit;

namespace RevitGit.Harness.Tests
{
    public sealed class CliParserTests
    {
        [Fact]
        public void Parse_ScenarioOptions_AreOrderIndependent()
        {
            var command = CliParser.Parse(new[] { "scenario", "branching", "--keep", "--json", "--verbose", "--workspace", "C:\\safe" });

            Assert.Equal(CommandKind.Scenario, command.Kind);
            Assert.Equal("branching", command.Name);
            Assert.True(command.Keep);
            Assert.True(command.Json);
            Assert.True(command.Verbose);
            Assert.Equal("C:\\safe", command.WorkspaceRoot);
        }

        [Fact]
        public void Parse_UnknownOption_IsInvalidArguments()
        {
            var exception = Assert.Throws<CliUsageException>(() => CliParser.Parse(new[] { "help", "--wat" }));
            Assert.Contains("Unknown option", exception.Message);
        }

        [Fact]
        public void ScenarioRegistry_ContainsEveryEpic7Scenario()
        {
            var expected = new[] { "linear-history", "branching", "switch-variant", "restore", "restore-in-variant", "restore-cross-variant", "compare", "reopen", "move-repository", "binary-roundtrip", "integrity", "many-versions", "topology-mismatch" };
            Assert.Equal(expected, ScenarioRegistry.Names.ToArray());
        }

        [Fact]
        public void ExitCodes_AreStable()
        {
            Assert.Equal(0, (int)ExitCode.Success);
            Assert.Equal(1, (int)ExitCode.ScenarioFailed);
            Assert.Equal(2, (int)ExitCode.InvalidArguments);
            Assert.Equal(3, (int)ExitCode.InfrastructureFailure);
        }

        [Fact]
        public void Help_OutputIsDeterministicAndListsDiagnosticsCommands()
        {
            var output = new StringWriter();
            var exit = new HarnessApplication(output).Execute(CliParser.Parse(new[] { "help" }));

            Assert.Equal(ExitCode.Success, exit);
            Assert.Contains("scenario <name>", output.ToString());
            Assert.Contains("validate <path-to-rfa>", output.ToString());
            Assert.Contains("diagnostics", output.ToString());
        }

        [Fact]
        public void JsonScenario_EmitsJsonWithoutHumanOutput()
        {
            var output = new StringWriter();
            var exit = new HarnessApplication(output).Execute(
                CliParser.Parse(new[] { "scenario", "linear-history", "--json" }));

            var text = output.ToString().Trim();
            Assert.Equal(ExitCode.Success, exit);
            Assert.StartsWith("{\"scenario\":\"linear-history\"", text);
            Assert.DoesNotContain("SCENARIO:", text);
            Assert.DoesNotContain("RESULT:", text);
        }
    }
}
