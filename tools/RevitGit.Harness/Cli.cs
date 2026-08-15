using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace RevitGit.Harness
{
    public enum ExitCode { Success = 0, ScenarioFailed = 1, InvalidArguments = 2, InfrastructureFailure = 3 }
    public enum CommandKind { Help, Scenario, Validate, Inspect, Compare, Diagnostics }

    public sealed class CliUsageException : Exception
    {
        public CliUsageException(string message) : base(message) { }
    }

    public sealed class ScenarioAssertionException : Exception
    {
        public ScenarioAssertionException(string message) : base(message) { }
    }

    public sealed class CliCommand
    {
        public CommandKind Kind { get; internal set; }
        public string Name { get; internal set; }
        public IReadOnlyList<string> Arguments { get; internal set; }
        public bool Keep { get; internal set; }
        public bool Json { get; internal set; }
        public bool Verbose { get; internal set; }
        public string WorkspaceRoot { get; internal set; }
    }

    public static class CliParser
    {
        public static CliCommand Parse(string[] args)
        {
            if (args == null || args.Length == 0) return New(CommandKind.Help);
            var positionals = new List<string>();
            var result = new CliCommand { Arguments = positionals };
            for (var index = 0; index < args.Length; index++)
            {
                var value = args[index];
                if (value == "--keep") result.Keep = true;
                else if (value == "--json") result.Json = true;
                else if (value == "--verbose") result.Verbose = true;
                else if (value == "--workspace")
                {
                    if (++index >= args.Length || string.IsNullOrWhiteSpace(args[index]))
                        throw new CliUsageException("--workspace requires a path.");
                    result.WorkspaceRoot = Path.GetFullPath(args[index]);
                }
                else if (value.StartsWith("--", StringComparison.Ordinal))
                    throw new CliUsageException("Unknown option: " + value);
                else positionals.Add(value);
            }

            if (positionals.Count == 0) result.Kind = CommandKind.Help;
            else
            {
                switch (positionals[0])
                {
                    case "help": result.Kind = CommandKind.Help; RequireCount(positionals, 1); break;
                    case "scenario": result.Kind = CommandKind.Scenario; RequireCount(positionals, 2); result.Name = positionals[1]; break;
                    case "validate": result.Kind = CommandKind.Validate; RequireCount(positionals, 2); break;
                    case "inspect": result.Kind = CommandKind.Inspect; RequireCount(positionals, 2); break;
                    case "compare": result.Kind = CommandKind.Compare; RequireCount(positionals, 4); break;
                    case "diagnostics": result.Kind = CommandKind.Diagnostics; RequireCount(positionals, 1); break;
                    default: throw new CliUsageException("Unknown command: " + positionals[0]);
                }
            }

            if (result.Kind != CommandKind.Scenario && (result.Keep || result.WorkspaceRoot != null))
                throw new CliUsageException("--keep and --workspace are only valid for scenario commands.");
            return result;
        }

        private static CliCommand New(CommandKind kind) => new CliCommand { Kind = kind, Arguments = new string[0] };

        private static void RequireCount(ICollection<string> values, int expected)
        {
            if (values.Count != expected) throw new CliUsageException("Invalid number of command arguments.");
        }
    }

    public static class ScenarioRegistry
    {
        private static readonly string[] Registered =
        {
            "linear-history", "branching", "switch-variant", "restore", "restore-in-variant", "compare",
            "reopen", "move-repository", "binary-roundtrip", "integrity", "many-versions", "topology-mismatch"
        };
        public static IReadOnlyList<string> Names => Registered;
        public static bool Contains(string name) => Registered.Contains(name, StringComparer.Ordinal);
    }

    public sealed class ScenarioReport
    {
        public ScenarioReport(string scenario, string workspace)
        {
            Scenario = scenario;
            Workspace = workspace;
            Steps = new List<ScenarioStep>();
            Lines = new List<string>();
        }
        public string Scenario { get; }
        public string Workspace { get; }
        public bool Success { get; internal set; }
        public List<ScenarioStep> Steps { get; }
        public List<string> Lines { get; }
        public string Failure { get; internal set; }
        public void Pass(string name) { Steps.Add(new ScenarioStep(name, true)); }
        public void Line(string value) { Lines.Add(value); }
    }

    [DataContract]
    public sealed class ScenarioStep
    {
        public ScenarioStep(string name, bool success) { Name = name; Success = success; }
        [DataMember(Name = "name", Order = 1)] public string Name { get; private set; }
        [DataMember(Name = "success", Order = 2)] public bool Success { get; private set; }
    }

    internal static class ScenarioAssert
    {
        public static void True(bool condition, string message)
        {
            if (!condition) throw new ScenarioAssertionException(message);
        }
        public static void Equal<T>(T expected, T actual, string message)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
                throw new ScenarioAssertionException(message + " Expected=" + expected + ", actual=" + actual + ".");
        }
        public static void Bytes(byte[] expected, byte[] actual, string message)
        {
            if (expected == null || actual == null || !expected.SequenceEqual(actual))
                throw new ScenarioAssertionException(message);
        }
    }

    internal static class OutputWriter
    {
        public static void WriteScenario(TextWriter output, ScenarioReport report, bool json, bool retained)
        {
            if (json) { output.WriteLine(ToJson(report, retained)); return; }
            output.WriteLine("SCENARIO: " + report.Scenario);
            output.WriteLine();
            foreach (var step in report.Steps) output.WriteLine("[PASS] " + step.Name);
            foreach (var line in report.Lines) output.WriteLine(line);
            if (!report.Success && report.Failure != null) output.WriteLine("[FAIL] " + report.Failure);
            output.WriteLine();
            output.WriteLine("RESULT: " + (report.Success ? "PASS" : "FAIL"));
            if (retained)
            {
                output.WriteLine("Workspace retained:");
                output.WriteLine(report.Workspace);
            }
        }

        private static string ToJson(ScenarioReport report, bool retained)
        {
            var dto = new ScenarioJson
            {
                Scenario = report.Scenario, Success = report.Success, Steps = report.Steps,
                Lines = report.Lines, Failure = report.Failure, Workspace = retained ? report.Workspace : null
            };
            using (var stream = new MemoryStream())
            {
                new DataContractJsonSerializer(typeof(ScenarioJson)).WriteObject(stream, dto);
                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }

        [DataContract]
        private sealed class ScenarioJson
        {
            [DataMember(Name = "scenario", Order = 1)] public string Scenario { get; set; }
            [DataMember(Name = "success", Order = 2)] public bool Success { get; set; }
            [DataMember(Name = "steps", Order = 3)] public List<ScenarioStep> Steps { get; set; }
            [DataMember(Name = "lines", Order = 4)] public List<string> Lines { get; set; }
            [DataMember(Name = "failure", Order = 5, EmitDefaultValue = false)] public string Failure { get; set; }
            [DataMember(Name = "workspace", Order = 6, EmitDefaultValue = false)] public string Workspace { get; set; }
        }
    }
}
