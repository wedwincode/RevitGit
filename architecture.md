# Family History — Architecture

## 1. Architectural goals

The architecture must optimize for:

- testability without Revit;
- deterministic behavior;
- offline operation;
- recoverability;
- isolation of Autodesk API dependencies;
- isolation of Git implementation details;
- simple user-facing concepts;
- future ability to support newer Revit versions without rewriting domain logic.

The central rule is:

> Revit API and LibGit2Sharp are adapters. Product behavior belongs in application/domain code.

## 2. Recommended solution structure

```text
FamilyHistory.sln

src/
  FamilyHistory.Domain/
  FamilyHistory.Application/
  FamilyHistory.Infrastructure.Git/
  RevitGit.Infrastructure.FileSystem/
  FamilyHistory.Revit2021/
  FamilyHistory.UI/

harness/
  FamilyHistory.Harness/

tests/
  FamilyHistory.Domain.Tests/
  FamilyHistory.Application.Tests/
  FamilyHistory.Infrastructure.Git.Tests/
  RevitGit.Infrastructure.FileSystem.Tests/
  FamilyHistory.UI.Tests/
  FamilyHistory.Contract.Tests/

fixtures/
  snapshots/
  repositories/

build/
  build-core.ps1
  test-core.ps1
  build-revit2021.ps1
  install-dev-addin.ps1

packaging/
  FamilyHistory.addin.template
  installer/
```

## 3. Target frameworks

### Revit-facing projects

`FamilyHistory.Revit2021` and WPF host projects must target .NET Framework 4.8 because Revit 2021 exposes its API through .NET Framework 4.8.

### Core projects

Preferred option for the first spike:

- `Domain`: `netstandard2.0`
- `Application`: `netstandard2.0`
- `Infrastructure.Storage`: `netstandard2.0` where practical
- `Infrastructure.Git`: target compatible framework selected after LibGit2Sharp compatibility spike; `net48` is acceptable
- `UI`: `net48` if it contains WPF types; keep ViewModels in a framework-neutral project if useful

Reason: a framework-neutral core allows fast headless testing and reduces coupling to Revit 2021.

If tooling friction becomes higher than the benefit, targeting all production projects to `net48` is acceptable, but `Autodesk.Revit.*` references must still remain restricted to the Revit adapter project.

## 4. Dependency direction

```text
                    +--------------------+
                    | FamilyHistory.UI   |
                    +----------+---------+
                               |
                               v
                    +--------------------+
                    | Application        |
                    +----+-----------+---+
                         |           |
                         v           v
                +-------------+  +------------------+
                | Domain      |  | Port interfaces  |
                +-------------+  +------------------+
                                     ^          ^
                                     |          |
                         +-----------+          +-------------+
                         |                                    |
              +----------------------+             +----------------------+
              | Infrastructure.Git   |             | Revit2021 Adapter    |
              +----------------------+             +----------------------+
```

Rules:

1. Domain references nothing else.
2. Application references Domain and defines ports/interfaces.
3. Infrastructure implements Application ports.
4. Revit2021 implements Revit-specific ports and references `RevitAPI.dll` / `RevitAPIUI.dll`.
5. UI calls Application use cases; UI does not contain Git commands or Revit data extraction logic.
6. No project except `FamilyHistory.Revit2021` may reference Autodesk Revit assemblies.

## 5. Domain model

Suggested core types:

```text
FamilyId
VariantId
VersionId
VersionNote
VersionRecord
Variant
HistoryGraph
FamilySnapshot
ParameterSnapshot
FamilyTypeSnapshot
NestedFamilySnapshot
FamilyDiff
Change<T>
```

Do not make Git SHA the domain `VersionId`. A domain ID may wrap/store the underlying Git object ID, but the application layer must not require Git semantics.

This makes later storage replacement possible and keeps UI vocabulary clean.

## 6. Application use cases

Each use case should be represented by a small command/query handler or service method with explicit inputs/outputs.

Required use cases:

```text
InitializeHistory
GetCurrentFamilyStatus
CreateVersion
GetHistoryGraph
GetVersionDetails
CompareVersions
RestoreVersionAsNew
CreateVariantFromVersion
SwitchVariant
```

Potential later use cases:

```text
ExportComparison
VerifyHistoryIntegrity
CompactHistory
RepairHistory
```

## 7. Ports

Application layer should depend on interfaces similar to:

```text
IFamilySnapshotProvider
IWorkingFamilyFile
IVersionRepository
IVariantRepository
IHistoryGraphReader
IClock
IFileSystem
ILogger
```

Possible combined storage port:

```text
IHistoryStore
  Initialize(...)
  CreateVersion(...)
  GetVersion(...)
  GetGraph(...)
  CreateVariant(...)
  MaterializeVersion(...)
```

Prefer behavior-oriented interfaces over one huge service.

## 8. Revit adapter

The Revit adapter is responsible only for translating Revit state into application-friendly data and performing narrowly defined Revit actions.

Responsibilities:

- detect whether active document is a family document;
- get family name/category;
- enumerate family parameters;
- extract formulas;
- enumerate family types and values;
- normalize Revit units into canonical snapshot values;
- extract nested-family metadata where reliable;
- save/synchronize the active `.rfa` according to chosen workflow;
- coordinate opening/reloading restored family files;
- expose Revit events to the application host.

Non-responsibilities:

- computing semantic diffs;
- deciding version graph semantics;
- creating Git branches/commits directly from UI event handlers;
- formatting user-facing history text;
- deciding restore safety policy.

## 9. Revit UI/threading model

Use WPF.

Preferred shell: Revit dockable pane.

Because a dockable/modeless pane cannot arbitrarily modify Revit documents, requests that need Revit API context must be marshaled through Revit's `ExternalEvent` mechanism.

Recommended flow:

```text
WPF View
   |
   v
ViewModel
   |
   v
Application command
   |
   +-- pure/non-Revit work can execute directly
   |
   +-- Revit-required operation
          |
          v
     IRevitRequestQueue
          |
          v
     ExternalEvent.Raise()
          |
          v
     IExternalEventHandler.Execute(...)
          |
          v
       Revit API
```

Do not call Revit API from arbitrary background threads.

Long-running pure work such as hashing, snapshot diffing, serialization, or Git reads may execute outside the Revit API callback when it does not touch Revit API objects. Never retain Revit API objects for use on worker threads; convert to neutral DTOs first.

## 10. Git adapter

### Library

Use LibGit2Sharp if the compatibility spike passes.

Do not require `git.exe` in normal deployment.

### Repository semantics

Suggested internal mapping:

```text
variant -> Git branch
version -> Git commit
current variant -> checked-out/current branch
version contents -> tracked working `.rfa` plus semantic snapshot file/objects
```

The exact on-disk representation should be hidden behind `IHistoryStore`.

### Binary strategy

For MVP, commit/store complete `.rfa` states through the Git-backed history adapter. Do not implement application-level binary deltas, custom delta chains, or a separate content-addressed binary store.

Repository growth must be measured with representative `.rfa` fixtures before any storage optimization is considered. Keep the storage behavior behind an interface so a later optimization does not affect Domain/Application semantics.

## 11. Filesystem storage layout

Required placement for v1:

```text
Door.rfa
.familyhistory/
  index.json
  repositories/
    <repository-guid>/
      repository.json
      history.json
      versions/
        <version-guid>/
          family.rfa
          snapshot.json
          version.json
```

The `.familyhistory` root is hidden and lives beside the working family. Each GUID
directory is one independent logical repository for one `.rfa`; `index.json` maps a
relative family filename to that repository ID. No absolute machine path is stored,
so moving the family and `.familyhistory` together preserves identity.

An external rename is reassociated automatically only when the adjacent index has a
single unambiguous repository. With multiple repositories, automatic reassociation
is deliberately deferred rather than guessing.

Filesystem JSON uses infrastructure DTOs and the .NET Framework
`DataContractJsonSerializer`; Domain models have no serialization attributes.
Critical JSON writes use a same-directory `.tmp-<guid>` file, flush it to disk, and
publish with replace/move. A version is built in a `.tmp-<guid>` directory and moved
to its final VersionId directory only after the binary, snapshot, checksums, and
metadata are complete. SHA-256 verifies `family.rfa` and `snapshot.json`.

## 12. Atomic version creation

Treat `CreateVersion` as a logical transaction even though Revit, filesystem, and Git do not share one database transaction.

Suggested sequence:

1. validate active family and history state;
2. request Revit to save the active family and confirm the working `.rfa` corresponds to current document state;
3. extract neutral snapshot;
4. serialize snapshot to temporary storage;
5. stage/store binary state;
6. create version in history store;
7. persist application state;
8. mark operation successful;
9. clean temporary artifacts.

On failure:

- never delete the user's working `.rfa`;
- clean temporary artifacts when safe;
- leave previous version graph intact;
- record diagnostic information.

## 13. Snapshot format

Snapshots require explicit schema versioning from day one.

Example:

```json
{
  "schemaVersion": 1,
  "family": {
    "name": "Door",
    "category": "Doors"
  },
  "parameters": [],
  "types": []
}
```

Rules:

- deterministic ordering before serialization;
- invariant/canonical numeric representation;
- explicit units or canonical units;
- no locale-dependent decimal formatting;
- distinguish null / absent / empty;
- backward-compatible readers when snapshot schema changes;
- golden-file tests for serialization.

For schema 1, parameter identity is a neutral, case-sensitive stable string key. The
Revit adapter will build it from the strongest identity available (shared GUID,
built-in identity, normalized definition identity, then a fallback composite key),
while the snapshot model remains independent of Revit types. Type values are keyed
by this stable key rather than by display name.

Numeric values use canonical human-scale units before entering the snapshot:
millimetres for length, square millimetres for area, cubic millimetres for volume,
degrees for angles, and unitless values for numbers. Schema 1 rounds all finite
floating-point values to six decimal places with midpoint rounding away from zero;
non-finite values are invalid. This normalization is centralized in the Domain
snapshot model.

## 14. Diff engine

The diff engine must be deterministic and pure.

Signature conceptually:

```text
FamilyDiff Compare(FamilySnapshot before, FamilySnapshot after)
```

No filesystem, Git, Revit, clock, or UI dependencies.

### Diff ordering

Stable ordering is required so tests and UI do not randomly reorder changes.

Recommended grouping:

1. family metadata;
2. parameters;
3. formulas;
4. types;
5. type values;
6. nested families;
7. elements;
8. geometry summary.

Within a group: stable key then display name.

The schema 1 diff implementation lives in `Application` as a pure, stateless
service over Domain snapshots. Parameters are matched by their case-sensitive
stable key, family types by their case-sensitive name, and type values by parameter
stable key. Collection changes carry `Added`, `Removed`, or `Modified` together
with their before/after states; modified parameters also expose field-level name,
data type, scope, and formula changes. No rename inference is performed.

Diff results are ordered deterministically by display name and then stable key for
parameters and type values, and by name for family types. Version-to-version
orchestration remains deferred until saved snapshots can be retrieved through a
storage port; the current service compares two already-materialized snapshots.

## 15. Geometry strategy

Do not make full geometry diff a gate for MVP.

Phases:

### Phase 1

- no detailed geometry diff;
- optionally compute a deterministic geometry fingerprint and show `Geometry changed`.

Epic 9 implements the Phase 1 fingerprint in the Revit 2021 adapter. It gathers
geometry with fixed fine-detail options, recursively applies `GeometryInstance`
transforms, canonicalizes tessellated points into millimetres at schema precision,
sorts neutral descriptors, and hashes them with SHA-256. The extractor observes only
the current document geometry state and never switches `FamilyManager.CurrentType`;
per-type geometry regeneration is deferred. Parameter values for every family type
are still read directly through the read-only `FamilyType.As*` API.

### Phase 2

- element-level matching for known family form classes;
- report high-confidence property changes.

### Phase 3

- visual/geometry comparison if product value justifies the complexity.

Every heuristic geometry match should expose an internal confidence score. Low-confidence matches should be summarized rather than presented as exact facts.


## 16. Test architecture

### 16.1 Fast tests

Must run without Revit and without a user profile dependency.

Examples:

- `CompareVersions_WhenFormulaChanges_ReturnsReadableFormulaChange`
- `CreateVariantFromVersion_PointsNewVariantAtSelectedVersion`
- `RestoreAsNewVersion_DoesNotRemoveDescendants`
- `SnapshotSerialization_IsDeterministic`

### 16.2 Git integration tests

Use a fresh temporary directory per test.

Never use the developer's real repository or global Git configuration.

Inject identity/config explicitly into test repositories.

Test:

- initialize;
- create version;
- graph reconstruction;
- create variant;
- switch variant;
- move tip with safety ref;
- materialize old binary;
- reopen repository after process restart simulation;
- corruption/error handling where feasible.

### 16.3 Revit contract tests

Most Revit behavior should be expressed as mapping contracts that can be tested against neutral DTO fixtures.

The thin live adapter still requires smoke/integration tests in Revit 2021.

Create a dedicated developer command/harness in Revit that can:

- load a known fixture `.rfa`;
- extract snapshot;
- save snapshot JSON to a temp location;
- compare it with an approved expected snapshot;
- print a concise pass/fail report.

Do not require Revit for every agent edit cycle.

## 17. Harness strategy for Codex

Provide two harness levels.

### Core harness

A console executable that does not reference Revit:

```text
FamilyHistory.Harness.exe scenario <name>
```

Suggested scenarios:

```text
init-history
create-versions
compare-snapshots
branch-from-old-version
restore-as-new
roundtrip-repository
```

Each scenario runs in a temporary directory and exits non-zero on failure.

Epic 7 implements this developer tool at `tools/RevitGit.Harness`. Its manual composition root connects the Application use cases to the real filesystem and LibGit2Sharp adapters without Revit, UI, WPF, or an external `git.exe`. Scenarios use deterministic snapshots and time, retain failed workspaces, and validate the Epic 6 transition topology. Validation is detection-only: `history.json` remains canonical application history metadata/topology, while the Git graph is its synchronized backing mirror; no repair or Git-only migration is performed.

### Revit harness

A special developer-only Revit command, excluded from release UI if desired, that validates adapter extraction against fixture families.

This keeps the TDD loop fast while still checking the real Autodesk boundary periodically.

## 18. Build strategy

### `build/build-core.ps1`

Builds all projects that do not require Autodesk DLLs.

### `build/test-core.ps1`

Runs all headless tests and harness scenarios.

### `build/build-revit2021.ps1`

Requires a configured Revit 2021 installation path and builds the adapter/add-in.

Do not commit Autodesk DLLs to source control.

Resolve them from a property/environment variable, for example:

```text
REVIT_2021_DIR=C:\Program Files\Autodesk\Revit 2021
```

### `build/install-dev-addin.ps1`

Copies development output and generated `.addin` manifest to the user's Revit 2021 add-in folder.

Keep installation script idempotent.

## 19. Logging

Use an internal logging abstraction.

Log categories:

- startup/shutdown;
- active-family detection;
- snapshot extraction duration/result;
- history operations;
- file restore/materialization;
- errors with correlation/operation ID.

Never log entire sensitive model content unnecessarily.

## 20. Version compatibility

Create explicit boundaries so later versions can be added as:

```text
FamilyHistory.Revit2021
FamilyHistory.Revit2022
...
```

or by multi-targeting/adapters when practical.

Do not let Revit 2021-specific API types leak into Domain/Application public APIs.

## 21. Git-backed history storage (Epic 6)

Storage format 2 adds an embedded repository below each Epic 5 family repository:

```text
.familyhistory/
  index.json
  repositories/<repository-id>/
    repository.json
    history.json
    repo/
      .git/
      family.rfa
      snapshot.json
      version.json
```

`LibGit2VersionRepository` implements the existing neutral `IHistoryRepository` and
`IVersionContentStore` contracts. It delegates domain metadata persistence to the
filesystem history repository while making Git the permanent version-content store.
The legacy `versions/<version-id>` archive is not written when this adapter is used.
`history.json` remains the canonical domain metadata/topology store during this
transition; Git parent links and variant tips are a mandatory mirror validated on
every load, so divergence is reported as corruption rather than reconciled silently.

Each version commit contains exactly `family.rfa`, `snapshot.json`, and
`version.json`. The JSON metadata is the durable `VersionId` to object mapping and
contains parent, creation time, comment, and restore origin. Internal branches use
`variant/<VariantId in N format>` and never use the presentation name. The checked
out internal branch represents the current variant and normal operations reject a
detached state.

Historical content is read directly from immutable commit trees. Restore copies the
selected historical binary into the family file, then the normal save path creates a
new version on the current variant tip; no reset, revert, merge, or rebase operation
is used. LibGit2Sharp 0.31.0 and NativeBinaries 2.0.323 are deployed with the add-in;
the Windows x64 runtime is loaded from `lib/win32/x64/git2-3f4182d.dll` without an
external Git installation.

## 22. Revit Save Version vertical slice (Epic 10)

The production ribbon command uses `TransactionMode.Manual` but opens no Revit
transaction. A modal WPF comment dialog is shown first; cancellation returns before
any document or storage operation. The existing Application `SaveVersionUseCase`
then calls a Revit implementation of `IFamilyDocumentGateway.Save`, after which the
Git content adapter requests the neutral snapshot through `IFamilySnapshotProvider`.
This preserves the required `Document.Save -> snapshot -> binary/snapshot storage`
ordering without leaking `Autodesk.Revit.DB.Document` into Application.

`SaveVersionUseCase` initializes a missing history as the single first version and
uses the explicit initial variant name supplied by the Revit composition root. An
existing history is loaded and validated before appending, so persisted
`CurrentVariantId` remains authoritative. A Revit-side lazy composition adapter
opens or initializes `FamilyRepositoryManager` only after the Revit save succeeds,
then composes `FileSystemHistoryRepository` and `LibGit2VersionRepository`. A final
load validates the transition `history.json`/Git topology; no repair is attempted.

## 23. History pane (Epic 11)

`RevitGit.UI` contains the WPF `HistoryView`, a headless-testable `HistoryViewModel`, and presentation-only version items. The UI assembly references Application/Domain but does not reference Autodesk Revit, LibGit2Sharp, or repository filesystem implementations.

The existing Application `GetHistoryUseCase` is the canonical read path. It follows the current variant tip through parent versions and returns that ancestry newest-first. It also resolves a restored version's source timestamp before data reaches presentation code. The query reads history metadata/topology only; it does not materialize `.rfa` content, load snapshots, or run semantic comparison.

`RevitGit.Revit2021` registers one dockable pane per Revit session under the fixed pane ID `9F0C2873-79CE-4C05-9C16-2B1FD7C8D9A1`. A session coordinator owns the pane, ViewModel, narrow refresh `ExternalEvent`, and Revit event subscriptions. The handler obtains the current `Document` only while executing in Revit context and maps it to no-document, non-family, unsaved-family, no-history, ready, or error presentation state. No Revit object is retained by the ViewModel.

Refresh requests are raised when the pane is shown, from the manual refresh command, after a successful Save Version, and on `ViewActivated`, `DocumentOpened`, and `DocumentClosed`. All subscriptions are removed and the external event is disposed during add-in shutdown. Repository corruption is reported as a stable user-facing error; no repair is attempted.

## 24. Compare in History pane (Epic 12)

Application exposes `CompareSavedVersionsUseCase` and `CompareVersionWithSnapshotUseCase` over the neutral `IVersionSnapshotStore`. Both validate `VersionId` against the complete family history, so versions from different variants may be compared without introducing variant restrictions. The Git adapter reads `snapshot.json` from the requested immutable version and deserializes it through a neutral delegate supplied by composition.

Saved-to-saved comparison runs without Revit API access. Saved-to-current comparison is raised through the History pane `ExternalEvent`; its handler revalidates the active family path, extracts a neutral live `FamilySnapshot`, and passes it to Application. It never calls `Document.Save`, opens a transaction, creates a Version, or reads current `.rfa` bytes. UI maps the returned `FamilyDiff` to Russian presentation models and does not recompute semantic changes.

## 25. Restore from History pane (Epic 13)

Restore uses a prepared/two-phase Application contract. `Prepare` validates complete history/Git topology, verifies that both the selected source and current-tip rollback content are readable, deserializes the canonical source snapshot, and materializes an immutable staged `.rfa` under the managed repository. It does not mutate history. `Finalize` reloads history, checks the expected current tip, stages source binary/snapshot directly from the immutable source version, and appends a normal one-parent restored version on the current variant.

The Revit coordinator does not save the dirty document and does not close an active document through `Document.Close`. It activates the staged family, closes the now-inactive original with `Close(false)`, atomically publishes the staged bytes at the original path, and finalizes history. It then posts Revit's official `PostableCommand.Close` for the active staged family. On the following `Idling` callback a second `ExternalEvent` opens and activates the original path, verifies it, and refreshes History. This prevents Revit from seeing the byte-identical staged and restored files as two simultaneously open documents. Intermediate document events are suppressed while restore is active. If finalize fails after replacement, the coordinator republishes the verified prior current version. If automatic reopen fails after successful finalize, disk content and history remain consistent and the UI reports the original path for manual reopening.

After reopen, the neutral live snapshot is compared with the canonical source snapshot. A mismatch is reported as a serious verification failure; it does not rewrite or repair history. The document lifecycle itself still requires a real Revit 2021 smoke test because compilation against the 2021 API cannot prove host behavior.
