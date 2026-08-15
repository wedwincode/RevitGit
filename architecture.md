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
  FamilyHistory.Infrastructure.Storage/
  FamilyHistory.Revit2021/
  FamilyHistory.UI/

harness/
  FamilyHistory.Harness/

tests/
  FamilyHistory.Domain.Tests/
  FamilyHistory.Application.Tests/
  FamilyHistory.Infrastructure.Git.Tests/
  FamilyHistory.Infrastructure.Storage.Tests/
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

## 11. Proposed on-disk layout

Required placement for v1:

```text
Door.rfa
.familyhistory/
  repo/
  state.json
  logs/
  temp/
```

The `.familyhistory` root is hidden and lives beside the working family so history can be copied/moved with the family data. The exact internal layout may change after the repository-scope spike.

Whether one `.familyhistory` repository tracks one family or multiple `.rfa` files in the same folder remains an explicit open decision. Centralized `%LOCALAPPDATA%` history is not the v1 design.

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

## 15. Geometry strategy

Do not make full geometry diff a gate for MVP.

Phases:

### Phase 1

- no detailed geometry diff;
- optionally compute a deterministic geometry fingerprint and show `Geometry changed`.

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
