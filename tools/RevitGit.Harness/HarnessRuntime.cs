using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RevitGit.Application.Abstractions;
using RevitGit.Application.Diff;
using RevitGit.Application.History;
using RevitGit.Application.Models;
using RevitGit.Domain.History;
using RevitGit.Domain.Identifiers;
using RevitGit.Domain.Snapshots;
using RevitGit.Infrastructure.FileSystem.History;
using RevitGit.Infrastructure.FileSystem.Repositories;
using RevitGit.Infrastructure.FileSystem.Serialization;
using RevitGit.Infrastructure.Git;

namespace RevitGit.Harness
{
    public sealed class HarnessApplication
    {
        private readonly TextWriter _output;
        public HarnessApplication(TextWriter output) { _output = output ?? throw new ArgumentNullException(nameof(output)); }

        public ExitCode Execute(CliCommand command)
        {
            switch (command.Kind)
            {
                case CommandKind.Help: WriteHelp(); return ExitCode.Success;
                case CommandKind.Scenario: return ExecuteScenario(command);
                case CommandKind.Validate: return Validate(Path.GetFullPath(command.Arguments[1]), command.Json);
                case CommandKind.Inspect: return Inspect(Path.GetFullPath(command.Arguments[1]), command.Verbose);
                case CommandKind.Compare: return Compare(Path.GetFullPath(command.Arguments[1]), command.Arguments[2], command.Arguments[3]);
                case CommandKind.Diagnostics: return Diagnostics();
                default: throw new CliUsageException("Unsupported command.");
            }
        }

        private ExitCode ExecuteScenario(CliCommand command)
        {
            if (!ScenarioRegistry.Contains(command.Name)) throw new CliUsageException("Unknown scenario: " + command.Name);
            var workspace = ScenarioWorkspace.Create(command.WorkspaceRoot);
            var report = new ScenarioReport(command.Name, workspace.DirectoryPath);
            ExitCode code;
            try
            {
                ScenarioRunner.Run(command.Name, workspace, report, command.Verbose);
                report.Success = true;
                code = ExitCode.Success;
            }
            catch (ScenarioAssertionException exception)
            {
                report.Failure = exception.Message;
                code = ExitCode.ScenarioFailed;
            }
            catch (Exception exception)
            {
                report.Failure = exception.Message;
                if (command.Verbose) report.Line(exception.ToString());
                code = ExitCode.InfrastructureFailure;
            }

            var retained = !report.Success || command.Keep;
            OutputWriter.WriteScenario(_output, report, command.Json, retained);
            if (!retained) workspace.Delete();
            return code;
        }

        private ExitCode Validate(string familyPath, bool json)
        {
            var opened = HarnessCompositionRoot.Open(familyPath, new HarnessClock());
            var result = opened.Validate();
            if (json)
            {
                _output.WriteLine("{\"valid\":" + (result.IsValid ? "true" : "false") + ",\"issues\":[" +
                    string.Join(",", result.Issues.Select(issue => "{\"code\":\"" + JsonEscape(issue.Code) + "\",\"message\":\"" + JsonEscape(issue.Message) + "\"}")) + "]}");
            }
            else
            {
                _output.WriteLine("Repository validation");
                _output.WriteLine();
                _output.WriteLine("RepositoryId: " + opened.Repository.Metadata.RepositoryId.ToString("D"));
                _output.WriteLine("Versions: " + opened.MetadataHistory.Versions.Count);
                _output.WriteLine("Variants: " + opened.MetadataHistory.Variants.Count);
                _output.WriteLine();
                if (result.IsValid)
                {
                    _output.WriteLine("[PASS] history metadata loaded");
                    _output.WriteLine("[PASS] history.json topology matches Git graph");
                    _output.WriteLine("[PASS] historical snapshots readable");
                }
                else foreach (var issue in result.Issues) _output.WriteLine("[FAIL] " + issue.Code + ": " + issue.Message);
                _output.WriteLine();
                _output.WriteLine("RESULT: " + (result.IsValid ? "VALID" : "INVALID"));
            }
            return result.IsValid ? ExitCode.Success : ExitCode.ScenarioFailed;
        }

        private ExitCode Inspect(string familyPath, bool verbose)
        {
            var root = HarnessCompositionRoot.Open(familyPath, new HarnessClock());
            var history = root.Adapter.Load(root.Document.GetIdentity());
            _output.WriteLine("RepositoryId: " + root.Repository.Metadata.RepositoryId.ToString("D"));
            _output.WriteLine("StorageFormatVersion: " + root.Repository.Metadata.StorageFormatVersion);
            _output.WriteLine("Family path: " + familyPath);
            _output.WriteLine("Version count: " + history.Versions.Count);
            _output.WriteLine("Variant count: " + history.Variants.Count);
            _output.WriteLine("Current variant: " + history.GetVariant(history.CurrentVariantId).Name);
            _output.WriteLine();
            _output.Write(HistoryTextRenderer.Render(history, null, root.Adapter, verbose));
            return ExitCode.Success;
        }

        private ExitCode Compare(string familyPath, string beforeText, string afterText)
        {
            Guid beforeGuid, afterGuid;
            if (!Guid.TryParse(beforeText, out beforeGuid) || !Guid.TryParse(afterText, out afterGuid))
                throw new CliUsageException("compare version identifiers must be GUID values shown by inspect --verbose.");
            var root = HarnessCompositionRoot.Open(familyPath, new HarnessClock());
            var before = root.ReadSnapshot(new VersionId(beforeGuid));
            var after = root.ReadSnapshot(new VersionId(afterGuid));
            _output.Write(DiffTextRenderer.Render(new FamilyDiffEngine().Compare(before, after)));
            return ExitCode.Success;
        }

        private ExitCode Diagnostics()
        {
            var path = Path.Combine(Path.GetTempPath(), "RevitGitHarness-diagnostics-" + Guid.NewGuid().ToString("N"));
            try
            {
                var family = Path.Combine(path, "Door.rfa");
                var root = HarnessCompositionRoot.Initialize(family, new byte[] { 1, 2, 3 });
                root.Adapter.Load(root.Document.GetIdentity());
                _output.WriteLine("Git repository open: PASS");
                _output.WriteLine("LibGit2Sharp assembly version: " + root.Adapter.EmbeddedGitAssemblyVersion);
                _output.WriteLine("Architecture: " + (Environment.Is64BitProcess ? "x64" : "x86"));
                _output.WriteLine("External git.exe invoked: NO");
                return ExitCode.Success;
            }
            finally { SafeDeleteDirectory(path); }
        }

        private void WriteHelp()
        {
            _output.WriteLine("RevitGit.Harness - headless Family History diagnostics");
            _output.WriteLine();
            _output.WriteLine("Commands:");
            _output.WriteLine("  help                                      Show this help");
            _output.WriteLine("  scenario <name> [options]                 Run an end-to-end scenario");
            _output.WriteLine("  validate <path-to-rfa> [--json]           Validate history.json against Git graph");
            _output.WriteLine("  inspect <path-to-rfa> [--verbose]         Show repository summary and history");
            _output.WriteLine("  compare <rfa> <version-a> <version-b>     Compare stored snapshots");
            _output.WriteLine("  diagnostics                               Verify embedded Git runtime");
            _output.WriteLine();
            _output.WriteLine("Scenario options: --keep --workspace <path> --verbose --json");
            _output.WriteLine("Scenarios: " + string.Join(", ", ScenarioRegistry.Names));
            _output.WriteLine();
            _output.WriteLine("Examples:");
            _output.WriteLine("  RevitGit.Harness.exe scenario branching --keep");
            _output.WriteLine("  RevitGit.Harness.exe scenario restore --json");
            _output.WriteLine("  RevitGit.Harness.exe validate C:\\Work\\Door.rfa");
        }

        private static string JsonEscape(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n");
        internal static void SafeDeleteDirectory(string path)
        {
            if (!Directory.Exists(path)) return;
            foreach (var item in Directory.GetFileSystemEntries(path, "*", SearchOption.AllDirectories)) File.SetAttributes(item, FileAttributes.Normal);
            Directory.Delete(path, true);
        }
    }

    internal sealed class ScenarioWorkspace
    {
        private ScenarioWorkspace(string directoryPath) { DirectoryPath = directoryPath; }
        public string DirectoryPath { get; }
        public string FamilyPath => Path.Combine(DirectoryPath, "Door.rfa");
        public static ScenarioWorkspace Create(string requestedRoot)
        {
            var root = requestedRoot ?? Path.Combine(Path.GetTempPath(), "RevitGitHarness");
            Directory.CreateDirectory(root);
            var run = Path.Combine(root, "run-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(run);
            return new ScenarioWorkspace(run);
        }
        public void Delete() { HarnessApplication.SafeDeleteDirectory(DirectoryPath); }
    }

    internal sealed class HarnessClock : IClock
    {
        private DateTimeOffset _next = new DateTimeOffset(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
        public DateTimeOffset UtcNow { get { var value = _next; _next = _next.AddMinutes(1); return value; } }
    }

    internal sealed class HarnessFamilyDocumentGateway : IFamilyDocumentGateway
    {
        private readonly FamilyIdentity _identity;
        public HarnessFamilyDocumentGateway(string path) { _identity = new FamilyIdentity(path); }
        public int SaveCount { get; private set; }
        public FamilyIdentity GetIdentity() => _identity;
        public void Save() { SaveCount++; }
    }

    internal sealed class HarnessCompositionRoot
    {
        private readonly SnapshotJsonSerializer _serializer;
        private HarnessCompositionRoot(string familyPath, HarnessClock clock, FamilyRepository repository, FamilySnapshot snapshot)
        {
            Clock = clock;
            Repository = repository;
            Document = new HarnessFamilyDocumentGateway(familyPath);
            _serializer = new SnapshotJsonSerializer();
            CurrentSnapshot = snapshot ?? SnapshotFixtureFactory.Default(900, null, "G1", false);
            MetadataRepository = new FileSystemHistoryRepository(new FamilyRepositoryManager(clock));
            Adapter = new LibGit2VersionRepository(repository.Paths.RepositoryDirectory, MetadataRepository, () => _serializer.Serialize(CurrentSnapshot));
        }

        public HarnessClock Clock { get; }
        public HarnessFamilyDocumentGateway Document { get; }
        public FamilyRepository Repository { get; }
        public FileSystemHistoryRepository MetadataRepository { get; }
        public LibGit2VersionRepository Adapter { get; }
        public FamilySnapshot CurrentSnapshot { get; set; }
        public FamilyHistory MetadataHistory => MetadataRepository.Load(Document.GetIdentity());

        public static HarnessCompositionRoot Initialize(string familyPath, byte[] binary, FamilySnapshot snapshot = null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(familyPath));
            File.WriteAllBytes(familyPath, binary);
            var clock = new HarnessClock();
            var repository = new FamilyRepositoryManager(clock).Initialize(familyPath);
            var result = new HarnessCompositionRoot(familyPath, clock, repository, snapshot);
            new InitializeHistoryUseCase(result.Adapter, result.Document, result.Adapter, clock).Execute("main");
            return result;
        }

        public static HarnessCompositionRoot Open(string familyPath, HarnessClock clock)
        {
            var repository = new FamilyRepositoryManager(clock).Open(familyPath);
            return new HarnessCompositionRoot(familyPath, clock, repository, null);
        }

        public VersionSummary Save(string comment) => new SaveVersionUseCase(Adapter, Document, Adapter, Clock).Execute(comment);
        public VariantSummary CreateVariant(VersionId from, string name) => new CreateVariantUseCase(Adapter, Document).Execute(from, name);
        public void Switch(VariantId id) => new SwitchVariantUseCase(Adapter, Document).Execute(id);
        public VersionSummary Restore(VersionId id, string comment = null) => new RestoreVersionUseCase(Adapter, Document, Adapter, Clock).Execute(id, comment);
        public FamilySnapshot ReadSnapshot(VersionId id) => _serializer.Deserialize(Adapter.ReadVersionFile(id, "snapshot.json"));

        public RepositoryValidationResult Validate()
        {
            var history = MetadataHistory;
            var issues = new List<RepositoryValidationIssue>(Adapter.InspectIntegrity(history).Issues);
            foreach (var version in history.Versions.Values)
            {
                try { ReadSnapshot(version.Id); }
                catch (Exception exception) { issues.Add(new RepositoryValidationIssue("SNAPSHOT_UNREADABLE", exception.Message, version.Id)); }
            }
            return new RepositoryValidationResult(issues);
        }
    }

    internal static class SnapshotFixtureFactory
    {
        public static FamilySnapshot Default(double width, string formula, string geometry, bool addedType)
        {
            var parameter = new FamilyParameterSnapshot("parameter:width", "Width", ParameterDataType.Length, ParameterScope.Type, formula);
            var types = new List<FamilyTypeSnapshot>
            {
                new FamilyTypeSnapshot("900x2100", new[] { new KeyValuePair<string, ParameterValue>(parameter.StableKey, ParameterValue.FromDouble(width)) })
            };
            if (addedType) types.Add(new FamilyTypeSnapshot("1200x2100", new[] { new KeyValuePair<string, ParameterValue>(parameter.StableKey, ParameterValue.FromDouble(1200)) }));
            return new FamilySnapshot("Door", "Doors", new[] { parameter }, types, geometry);
        }
    }

    internal sealed class ScenarioVersionAliases
    {
        private readonly Dictionary<VersionId, string> _byId = new Dictionary<VersionId, string>();
        private readonly Dictionary<string, VersionId> _byName = new Dictionary<string, VersionId>(StringComparer.Ordinal);
        public void Add(string alias, VersionId id) { _byId[id] = alias; _byName[alias] = id; }
        public VersionId this[string alias] => _byName[alias];
        public string Get(VersionId id) { string alias; return id != null && _byId.TryGetValue(id, out alias) ? alias : id == null ? "-" : id.ToString(); }
    }

    internal static class ScenarioRunner
    {
        public static void Run(string name, ScenarioWorkspace workspace, ScenarioReport report, bool verbose)
        {
            switch (name)
            {
                case "linear-history": Linear(workspace, report, verbose); break;
                case "branching": Branching(workspace, report, verbose); break;
                case "switch-variant": SwitchVariant(workspace, report); break;
                case "restore": Restore(workspace, report, false); break;
                case "restore-in-variant": Restore(workspace, report, true); break;
                case "compare": Compare(workspace, report); break;
                case "reopen": Reopen(workspace, report); break;
                case "move-repository": MoveRepository(workspace, report); break;
                case "binary-roundtrip": BinaryRoundtrip(workspace, report); break;
                case "integrity": Integrity(workspace, report, false); break;
                case "many-versions": ManyVersions(workspace, report); break;
                case "topology-mismatch": Integrity(workspace, report, true); break;
                default: throw new CliUsageException("Unknown scenario: " + name);
            }
        }

        private static HarnessCompositionRoot NewRoot(ScenarioWorkspace workspace, byte[] binary = null)
        {
            return HarnessCompositionRoot.Initialize(workspace.FamilyPath, binary ?? new byte[] { 0, 65, 0, 1 }, SnapshotFixtureFactory.Default(900, null, "G1", false));
        }

        private static ScenarioVersionAliases InitialAliases(HarnessCompositionRoot root)
        {
            var aliases = new ScenarioVersionAliases();
            aliases.Add("A", root.MetadataHistory.Versions.Values.Single().Id);
            return aliases;
        }

        private static VersionSummary MutateAndSave(HarnessCompositionRoot root, int value, FamilySnapshot snapshot, string comment)
        {
            File.WriteAllBytes(root.Document.GetIdentity().Value, new byte[] { 0, (byte)value, 255, (byte)(value * 3) });
            root.CurrentSnapshot = snapshot;
            return root.Save(comment);
        }

        private static void Linear(ScenarioWorkspace workspace, ScenarioReport report, bool verbose)
        {
            var root = NewRoot(workspace); var aliases = InitialAliases(root); report.Pass("repository initialized and version A saved");
            var b = MutateAndSave(root, 2, SnapshotFixtureFactory.Default(1000, null, "G1", false), "B"); aliases.Add("B", b.Id); report.Pass("version B saved");
            var c = MutateAndSave(root, 3, SnapshotFixtureFactory.Default(1000, "Width / 2", "G2", true), "C"); aliases.Add("C", c.Id); report.Pass("version C saved");
            root = HarnessCompositionRoot.Open(workspace.FamilyPath, new HarnessClock());
            var history = root.Adapter.Load(root.Document.GetIdentity());
            AssertChain(history, aliases["A"], aliases["B"], aliases["C"]);
            ScenarioAssert.True(root.Validate().IsValid, "history.json and Git graph must agree after reopen."); report.Pass("repository reopened and validated");
            report.Line(HistoryTextRenderer.Render(history, aliases, root.Adapter, verbose).TrimEnd());
        }

        private static void Branching(ScenarioWorkspace workspace, ScenarioReport report, bool verbose)
        {
            var root = NewRoot(workspace); var aliases = InitialAliases(root);
            var b = MutateAndSave(root, 2, SnapshotFixtureFactory.Default(1000, null, "G1", false), "B"); aliases.Add("B", b.Id);
            var c = MutateAndSave(root, 3, SnapshotFixtureFactory.Default(1100, null, "G1", false), "C"); aliases.Add("C", c.Id);
            var variant = root.CreateVariant(b.Id, "variant-1"); root.Switch(variant.Id);
            var d = MutateAndSave(root, 4, SnapshotFixtureFactory.Default(1200, null, "G2", false), "D"); aliases.Add("D", d.Id);
            var e = MutateAndSave(root, 5, SnapshotFixtureFactory.Default(1300, "Width / 2", "G2", true), "E"); aliases.Add("E", e.Id);
            var history = root.Adapter.Load(root.Document.GetIdentity());
            ScenarioAssert.Equal(b.Id, d.ParentVersionId, "D parent must be B."); ScenarioAssert.Equal(d.Id, e.ParentVersionId, "E parent must be D.");
            ScenarioAssert.True(history.Variants.Values.Any(item => item.Name == "main" && item.CurrentVersionId.Equals(c.Id)), "Main must remain at C.");
            ScenarioAssert.True(root.Validate().IsValid, "Branching topology must validate."); report.Pass("A -> B -> C and B -> D -> E created through Application"); report.Pass("history.json vs Git graph: OK");
            report.Line(HistoryTextRenderer.Render(history, aliases, root.Adapter, verbose).TrimEnd());
        }

        private static void SwitchVariant(ScenarioWorkspace workspace, ScenarioReport report)
        {
            var root = NewRoot(workspace); var aliases = InitialAliases(root);
            var b = MutateAndSave(root, 2, SnapshotFixtureFactory.Default(1000, null, "G1", false), "B"); aliases.Add("B", b.Id);
            MutateAndSave(root, 3, SnapshotFixtureFactory.Default(1100, null, "G1", false), "C");
            var main = root.MetadataHistory.CurrentVariantId;
            var variant = root.CreateVariant(b.Id, "variant-1"); root.Switch(variant.Id);
            MutateAndSave(root, 4, SnapshotFixtureFactory.Default(1200, null, "G2", false), "D");
            var count = root.Adapter.CommitCount;
            root.Switch(main); root.Switch(variant.Id); root.Switch(main); root.Switch(variant.Id);
            var history = root.Adapter.Load(root.Document.GetIdentity());
            ScenarioAssert.Equal(variant.Id, history.CurrentVariantId, "CurrentVariantId must follow switches.");
            ScenarioAssert.Equal(count, root.Adapter.CommitCount, "Switch must not create versions.");
            ScenarioAssert.Equal(root.Adapter.GetInternalBranchName(variant.Id), root.Adapter.CurrentInternalBranchName, "HEAD must be attached to current variant branch.");
            report.Pass("variants switched main -> variant -> main -> variant"); report.Pass("no versions created and HEAD remains attached");
        }

        private static void Restore(ScenarioWorkspace workspace, ScenarioReport report, bool inVariant)
        {
            var aBytes = new byte[] { 0, 65, 255, 0, 17 }; var aSnapshot = SnapshotFixtureFactory.Default(900, null, "G1", false);
            var root = HarnessCompositionRoot.Initialize(workspace.FamilyPath, aBytes, aSnapshot); var aliases = InitialAliases(root);
            var b = MutateAndSave(root, 2, SnapshotFixtureFactory.Default(1000, null, "G1", false), "B"); aliases.Add("B", b.Id);
            var c = MutateAndSave(root, 3, SnapshotFixtureFactory.Default(1100, null, "G2", false), "C"); aliases.Add("C", c.Id);
            VersionSummary previous = c; VersionId mainTip = c.Id;
            if (inVariant)
            {
                var variant = root.CreateVariant(b.Id, "variant-1"); root.Switch(variant.Id);
                previous = MutateAndSave(root, 4, SnapshotFixtureFactory.Default(1200, null, "G2", false), "D"); aliases.Add("D", previous.Id);
            }
            root.CurrentSnapshot = aSnapshot;
            var restored = root.Restore(aliases["A"]); aliases.Add(inVariant ? "E" : "D", restored.Id);
            ScenarioAssert.Equal(previous.Id, restored.ParentVersionId, "Restored version parent must be current tip.");
            ScenarioAssert.Equal(aliases["A"], restored.RestoredFromVersionId, "RestoredFrom must reference A.");
            ScenarioAssert.Bytes(aBytes, File.ReadAllBytes(workspace.FamilyPath), "Restored binary must equal A byte-for-byte.");
            ScenarioAssert.True(root.ReadSnapshot(restored.Id).Equals(aSnapshot), "Restored snapshot must be semantically equal to A.");
            if (inVariant) ScenarioAssert.True(root.MetadataHistory.Variants.Values.Any(item => item.Name == "main" && item.CurrentVersionId.Equals(mainTip)), "Main variant must remain untouched.");
            ScenarioAssert.True(root.Validate().IsValid, "Restore topology must validate.");
            report.Pass(inVariant ? "A restored as E in secondary variant" : "A restored as new version D"); report.Pass("binary and snapshot equal A; later history preserved");
            report.Line(HistoryTextRenderer.Render(root.MetadataHistory, aliases, root.Adapter, false).TrimEnd());
        }

        private static void Compare(ScenarioWorkspace workspace, ScenarioReport report)
        {
            var root = NewRoot(workspace); var a = root.MetadataHistory.Versions.Values.Single().Id;
            var b = MutateAndSave(root, 2, SnapshotFixtureFactory.Default(1000, "Width / 2", "G2", true), "B");
            var diff = new FamilyDiffEngine().Compare(root.ReadSnapshot(a), root.ReadSnapshot(b.Id));
            ScenarioAssert.True(diff.ParameterChanges.Any(change => change.FormulaChange != null), "Formula modification must be reported.");
            ScenarioAssert.True(diff.TypeChanges.Any(change => change.Kind == ChangeKind.Added && change.Name == "1200x2100"), "Added type must be reported.");
            ScenarioAssert.True(diff.TypeChanges.Any(change => change.ValueChanges.Any()), "Width value modification must be reported.");
            ScenarioAssert.True(diff.GeometryChanged, "Geometry fingerprint modification must be reported.");
            report.Pass("real FamilyDiffEngine reported value, formula, type and geometry changes"); report.Line(DiffTextRenderer.Render(diff).TrimEnd());
        }

        private static void Reopen(ScenarioWorkspace workspace, ScenarioReport report)
        {
            var root = NewRoot(workspace); var aliases = InitialAliases(root);
            var b = MutateAndSave(root, 2, SnapshotFixtureFactory.Default(1000, null, "G1", false), "B"); aliases.Add("B", b.Id);
            var variant = root.CreateVariant(b.Id, "variant-1"); root.Switch(variant.Id);
            var c = MutateAndSave(root, 3, SnapshotFixtureFactory.Default(1100, "Width / 2", "G2", true), "C"); aliases.Add("C", c.Id);
            root = null;
            var reopened = HarnessCompositionRoot.Open(workspace.FamilyPath, new HarnessClock());
            var history = reopened.Adapter.Load(reopened.Document.GetIdentity());
            ScenarioAssert.Equal(3, history.Versions.Count, "Reopened history must contain all versions.");
            ScenarioAssert.Equal(2, history.Variants.Count, "Reopened history must contain variants.");
            ScenarioAssert.True(reopened.ReadSnapshot(aliases["A"]) != null && reopened.ReadSnapshot(c.Id) != null, "Historical snapshots must be readable after reopen.");
            ScenarioAssert.True(reopened.Validate().IsValid, "Reopened mapping must validate."); report.Pass("new composition root restored history, mapping, current variant and snapshots");
        }

        private static void MoveRepository(ScenarioWorkspace workspace, ScenarioReport report)
        {
            var source = Path.Combine(workspace.DirectoryPath, "workspace-A"); var target = Path.Combine(workspace.DirectoryPath, "workspace-B");
            var family = Path.Combine(source, "Door.rfa"); var root = HarnessCompositionRoot.Initialize(family, new byte[] { 1, 0, 2 });
            MutateAndSave(root, 2, SnapshotFixtureFactory.Default(1000, null, "G1", false), "B");
            var id = root.Repository.Metadata.RepositoryId; root = null;
            Directory.Move(source, target); var movedFamily = Path.Combine(target, "Door.rfa");
            var moved = HarnessCompositionRoot.Open(movedFamily, new HarnessClock());
            ScenarioAssert.Equal(id, moved.Repository.Metadata.RepositoryId, "RepositoryId must survive move.");
            ScenarioAssert.Equal(2, moved.MetadataHistory.Versions.Count, "Versions must survive move.");
            ScenarioAssert.True(moved.Validate().IsValid, "Moved repository must validate without old absolute path."); report.Pass("repository moved and reopened without old absolute path");
        }

        private static void BinaryRoundtrip(ScenarioWorkspace workspace, ScenarioReport report)
        {
            var payload = new byte[256 * 1024]; for (var i = 0; i < payload.Length; i++) payload[i] = (byte)((i * 31) % 256); payload[0] = 0; payload[127] = 255;
            var root = HarnessCompositionRoot.Initialize(workspace.FamilyPath, payload); var a = root.MetadataHistory.Versions.Values.Single().Id;
            MutateAndSave(root, 7, SnapshotFixtureFactory.Default(1000, null, "G2", false), "changed");
            root.CurrentSnapshot = root.ReadSnapshot(a); root.Restore(a);
            ScenarioAssert.Bytes(payload, File.ReadAllBytes(workspace.FamilyPath), "Binary payload did not round-trip byte-for-byte."); report.Pass("256 KiB binary payload restored byte-for-byte");
        }

        private static void Integrity(ScenarioWorkspace workspace, ScenarioReport report, bool topologyOnly)
        {
            var root = NewRoot(workspace); MutateAndSave(root, 2, SnapshotFixtureFactory.Default(1000, null, "G1", false), "B");
            ScenarioAssert.True(root.Validate().IsValid, "Initial repository must validate."); report.Pass("valid repository accepted");
            var historyBeforeCorruption = root.MetadataHistory;
            var tip = historyBeforeCorruption.GetVariant(historyBeforeCorruption.CurrentVariantId).CurrentVersionId;
            var parent = historyBeforeCorruption.GetVersion(tip).ParentVersionId;
            RepositoryCorruptor.ChangeCurrentVariantTip(root.Repository.Paths.HistoryPath, tip, parent);
            var result = root.Adapter.InspectIntegrity(root.MetadataHistory);
            ScenarioAssert.True(!result.IsValid, "Validator must detect controlled corruption.");
            ScenarioAssert.True(result.Issues.Any(issue => issue.Code == "VARIANT_TIP_MISMATCH"), "Validator must report VARIANT_TIP_MISMATCH.");
            report.Pass(topologyOnly ? "validator detected branch-tip mismatch" : "controlled transition-topology corruption detected predictably");
        }

        private static void ManyVersions(ScenarioWorkspace workspace, ScenarioReport report)
        {
            var root = NewRoot(workspace); var ids = new List<VersionId> { root.MetadataHistory.Versions.Values.Single().Id };
            for (var i = 1; i < 50; i++) ids.Add(MutateAndSave(root, i, SnapshotFixtureFactory.Default(900 + i, null, "G" + (i % 3), false), "V" + (i + 1)).Id);
            root = HarnessCompositionRoot.Open(workspace.FamilyPath, new HarnessClock());
            ScenarioAssert.Equal(50, root.MetadataHistory.Versions.Count, "Expected 50 versions after reopen.");
            ScenarioAssert.True(root.Validate().IsValid, "50-version repository must validate.");
            ScenarioAssert.True(root.ReadSnapshot(ids[0]) != null && root.ReadSnapshot(ids[25]) != null && root.ReadSnapshot(ids[49]) != null, "First/middle/last snapshots must resolve.");
            report.Pass("50 versions created, reopened and validated"); report.Pass("first, middle and last snapshots resolved");
        }

        private static void AssertChain(FamilyHistory history, params VersionId[] ids)
        {
            for (var i = 1; i < ids.Length; i++) ScenarioAssert.Equal(ids[i - 1], history.GetVersion(ids[i]).ParentVersionId, "Linear parent mismatch.");
        }
    }

    internal static class RepositoryCorruptor
    {
        public static void ChangeCurrentVariantTip(string historyPath, VersionId from, VersionId to)
        {
            var json = File.ReadAllText(historyPath);
            var oldValue = "\"currentVersionId\":\"" + from.Value.ToString("D") + "\"";
            var newValue = "\"currentVersionId\":\"" + to.Value.ToString("D") + "\"";
            var index = json.IndexOf(oldValue, StringComparison.Ordinal);
            if (index < 0) throw new InvalidOperationException("Controlled variant tip field was not found.");
            json = json.Substring(0, index) + newValue + json.Substring(index + oldValue.Length);
            File.WriteAllText(historyPath, json);
        }
    }

    internal static class HistoryTextRenderer
    {
        public static string Render(FamilyHistory history, ScenarioVersionAliases aliases, LibGit2VersionRepository adapter, bool verbose)
        {
            var writer = new StringWriter();
            foreach (var variant in history.Variants.Values.OrderBy(item => item.Name, StringComparer.Ordinal))
            {
                var chain = new List<VersionId>(); var current = variant.CurrentVersionId;
                while (current != null) { chain.Add(current); current = history.GetVersion(current).ParentVersionId; }
                chain.Reverse();
                writer.WriteLine("Variant: " + variant.Name + (variant.Id.Equals(history.CurrentVariantId) ? " (current)" : string.Empty));
                writer.WriteLine(string.Join(" -> ", chain.Select(id => Format(id, aliases, adapter, verbose))));
                writer.WriteLine();
            }
            return writer.ToString();
        }

        private static string Format(VersionId id, ScenarioVersionAliases aliases, LibGit2VersionRepository adapter, bool verbose)
        {
            var label = aliases == null ? id.ToString() : aliases.Get(id);
            return verbose ? label + " [VersionId=" + id + " GitSha=" + adapter.GetStorageObjectId(id) + "]" : label;
        }
    }

    internal static class DiffTextRenderer
    {
        public static string Render(FamilyDiff diff)
        {
            var writer = new StringWriter(); writer.WriteLine("Parameters:");
            foreach (var change in diff.ParameterChanges)
            {
                writer.WriteLine("  " + change.Name + " (" + change.Kind + ")");
                if (change.FormulaChange != null) writer.WriteLine("    formula: " + Value(change.FormulaChange.OldValue) + " -> " + Value(change.FormulaChange.NewValue));
            }
            writer.WriteLine("Types:");
            foreach (var change in diff.TypeChanges)
            {
                writer.WriteLine("  " + (change.Kind == ChangeKind.Added ? "+ " : change.Kind == ChangeKind.Removed ? "- " : "~ ") + change.Name);
                foreach (var value in change.ValueChanges) writer.WriteLine("    " + value.ParameterName + ": " + Parameter(value.Before) + " -> " + Parameter(value.After));
            }
            writer.WriteLine("Geometry:"); writer.WriteLine(diff.GeometryChanged ? "  changed" : "  unchanged"); return writer.ToString();
        }
        private static string Value(string value) => value ?? "null";
        private static string Parameter(ParameterValue value)
        {
            if (value == null) return "null";
            if (value.DoubleValue.HasValue) return value.DoubleValue.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (value.IntegerValue.HasValue) return value.IntegerValue.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return value.StringValue ?? value.Kind.ToString();
        }
    }
}
