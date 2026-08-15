# Family History — Project Specification

## 1. Purpose

Family History is an offline Autodesk Revit 2021 add-in for versioning Revit family files (`.rfa`) without exposing Git terminology to end users.

The product solves a common workflow problem: during family development users create many manually numbered or suffixed copies such as `Door_01.rfa`, `Door_final.rfa`, `Door_final_2.rfa`, etc. Family History replaces that directory clutter with one working `.rfa` and an internal local history.

The add-in uses Git-compatible local storage internally, but the UI must present domain language understandable to non-programmers.

## 2. Target environment

- Autodesk Revit: 2021
- OS: Windows
- Revit add-in runtime: .NET Framework 4.8
- Language: C#
- UI: WPF
- Network: no network access is required at runtime
- Version storage: local filesystem in a hidden `.familyhistory` directory beside the working `.rfa`
- Git implementation: embedded library, not an external `git.exe` dependency
- Preferred Git library: LibGit2Sharp, subject to a compatibility spike on Revit 2021/.NET Framework 4.8

## 3. Product terminology

User-facing language must not expose Git unless an advanced diagnostic screen is introduced later.

| Internal concept | User-facing term |
|---|---|
| repository | history storage / history |
| commit | version |
| commit message | version note / comment |
| branch | variant |
| branch tip / HEAD | current version |
| checkout | open / switch to / restore |
| diff | changes / comparison |
| create branch from commit | create variant from this version |

Forbidden in normal UI copy for MVP: commit, SHA, HEAD, checkout, reset, rebase, merge, cherry-pick, detached HEAD.

## 4. MVP user workflows

### 4.1 Initialize history

When the user opens an `.rfa` that is not yet tracked, the add-in offers to create local history storage.

Expected result:

- one working `.rfa` remains visible to the user;
- history data is stored in a hidden/service directory;
- an initial version can be created explicitly;
- the add-in does not create manually numbered `.rfa` copies beside the working file.

### 4.2 Save a version

The user chooses `Save version`. The version note is optional.

The add-in:

1. saves the active family automatically through the Revit adapter;
2. creates a semantic snapshot from the family through Revit API;
3. stores the `.rfa` state in the local version store;
4. stores normalized snapshot metadata;
5. creates a version record atomically from the application point of view;
6. refreshes the history UI.

### 4.3 View history

The user sees chronological versions for the selected variant.

Each entry should show at minimum:

- date/time;
- note;
- summary of changes from parent version;
- marker for current version.

### 4.4 Compare versions

The user can select any two versions and view a semantic comparison.

Initial comparison categories:

1. family identity/category;
2. family parameters;
3. parameter formulas;
4. family types;
5. parameter values by family type;
6. nested family references where reliably obtainable;
7. element-level summary where reliably obtainable;
8. geometry changed/not changed summary as an optional later MVP item.

The comparison UI must prefer domain-readable values over raw Revit storage values.

Example:

```text
Parameter: Width
900 mm -> 1000 mm

Formula: HandleHeight
Height / 2 -> Height * 0.45

Type added
1200 x 2100
```

Do not display raw `ElementId`, internal feet, storage enum values, or opaque identifiers unless the user opens diagnostics.

### 4.5 Restore an old version safely

Default restore must be non-destructive.

Required semantic behavior:

- retrieve the selected old `.rfa` state;
- make it the working state;
- create a new version representing the restoration;
- preserve all later history;
- generate an automatic restoration note if the user does not provide one, for example `Восстановлено из версии от 12.08.2026 11:10`.

Example internal history:

```text
A -- B -- C -- D -- E
     ^              |
     |              +-- E has content restored from B
     +---------------- selected source
```

This avoids deleting history and is the preferred normal-user action.

### 4.6 Create a new variant from an old version

The user selects a historical version and chooses `Create variant from this version`.

Internally a new Git branch is created at that version. In v1 a variant can be created and switched to. Rename, archive, delete, and merge operations are out of scope. A human-readable name is assigned when the variant is created.

Example:

```text
Main
A -- B -- C -- D
     \
      E -- F   Alternative handle
```

No merge functionality is required.


## 5. Explicitly out of scope for MVP

- merge;
- rebase;
- merge requests / pull requests;
- cherry-pick;
- remote repositories;
- cloud synchronization;
- concurrent multi-user editing;
- automatic semantic merge of `.rfa` files;
- arbitrary Git command UI;
- editing raw Git refs from the user interface;
- destructive branch-tip moves / reset-style history rewriting;
- variant rename/archive/delete in v1;
- complete CAD-level geometric diff;
- network dependency at runtime.

## 6. Semantic snapshot scope

The snapshot is a stable, normalized representation used for comparisons. It is not intended to reconstruct the `.rfa`; the binary `.rfa` remains the source for restoration.

Proposed snapshot schema categories:

```text
FamilySnapshot
  formatVersion
  family
    name
    category
  parameters[]
    stableKey
    displayName
    dataType
    scope
    isShared
    sharedGuid?
    formula?
  types[]
    name
    valuesByParameterKey
  nestedFamilies[]
  elementsSummary[]
  geometryFingerprint?
```

### Stable keys

Never use display name alone as identity when a stronger identifier exists.

Priority should be evaluated per entity type, for example:

1. shared parameter GUID when available;
2. built-in parameter identity when available;
3. normalized definition identity;
4. fallback composite key.

Element matching and geometry matching require a separate design spike and must not block parameter/type diff MVP.

## 7. Diff behavior

The diff engine is pure domain code. It receives two normalized `FamilySnapshot` values and returns `FamilyDiff`.

It must not reference `Autodesk.Revit.*` assemblies.

Example result model:

```text
FamilyDiff
  parameterChanges[]
  formulaChanges[]
  typeChanges[]
  typeValueChanges[]
  nestedFamilyChanges[]
  elementChanges[]
  geometryChangeSummary?
```

Every change should carry:

- category;
- change kind: added / removed / modified;
- stable identity;
- display label;
- old value;
- new value;
- optional confidence level when entity matching is heuristic.

## 8. UI direction

Use a Revit dockable WPF pane where practical.

Suggested sections:

### Current family

- family name;
- current variant;
- current version;
- dirty/unversioned changes indicator;
- `Save version` button.

### History graph

- graphical variant/version graph similar to a simplified IDE history graph;
- nodes represent versions;
- lines represent parent relationships;
- variant labels are visible;
- no raw SHA in normal mode.

### Version details

- date;
- note;
- changes summary;
- actions:
  - Compare
  - Restore as new version
  - Create variant from here

### Compare view

Two selectable versions with grouped semantic changes.

## 9. Reliability requirements

1. A failure while creating a version must not corrupt the working `.rfa`.
2. A failure while updating history must either roll back the operation or leave a recoverable state.
3. Restore must verify that the selected binary object exists and passes expected integrity checks before replacing/opening the working state.
4. History operations must use temporary files plus atomic replace/move where practical.
5. The add-in must keep diagnostic logs locally.
6. User-visible errors must be actionable and must not expose stack traces by default.
7. No destructive history operation may silently make versions inaccessible.

## 10. Testing goals

Development follows red-green-refactor where practical:

1. agent writes or updates a failing test;
2. agent runs the smallest relevant test set and records the expected failure;
3. agent writes production code;
4. agent reruns tests until green;
5. agent refactors without changing behavior;
6. agent runs the broader core suite.

Most logic must be executable without Revit installed or running.

Required test layers:

- unit tests for Domain;
- unit tests for Application use cases;
- Git repository integration tests against temporary directories;
- serialization compatibility tests for snapshots;
- UI ViewModel tests without Revit;
- Revit adapter smoke tests executed separately on a machine with Revit 2021.

## 11. Acceptance criteria for first usable release

A release is usable when a Revit 2021 user can, completely offline:

1. open an `.rfa`;
2. initialize history;
3. save multiple versions with optional notes;
4. see history without manually duplicated files;
5. compare parameter/formula/type changes between two versions;
6. restore an old version without destroying later history;
7. create a new variant from any historical version;
8. switch between variants;
9. close/reopen Revit and retain correct history state;
10. carry the history with the family when the working family and adjacent `.familyhistory` directory are copied/moved together.

## 12. Confirmed product decisions

The following decisions are fixed for the first implementation unless explicitly changed later:

1. Runtime is fully offline and single-user. Shared/network history is not supported in v1.
2. History is stored in a hidden `.familyhistory` directory beside the working `.rfa`.
3. `Save version` automatically saves the active Revit family first.
4. Version notes are optional. Date/time is always recorded. Author identity is not recorded.
5. Normal restore is non-destructive: it restores old binary content and creates a new version. No reset-style pointer move is exposed in v1.
6. Restoration receives an automatic human-readable note when no explicit note is supplied.
7. A variant is the user-facing representation of an internal branch. V1 supports create-from-version and switch only; rename/archive/delete/merge are out of scope.
8. Mandatory semantic diff for v1: parameters, formulas, family types, and parameter values by type.
9. Geometry diff for v1 may be limited to a reliable `Geometry changed` summary.
10. Binary delta optimization is explicitly out of scope. Store complete `.rfa` states and measure real repository growth before optimizing.
11. Revit 2021 is the only supported Revit release for v1.
12. UI may be Russian-only in v1. Localization infrastructure is not a release requirement.
13. History is portable only when the `.rfa` and its adjacent `.familyhistory` data are moved/copied together.
14. No telemetry, cloud sync, remote repositories, or runtime network dependency.
15. Each `.rfa` family has one independent logical history repository. Multiple
    repositories may share the adjacent technical `.familyhistory` root, but never
    share history state.

## 13. Open product decisions

1. **Variant naming:** what default name should be proposed when a new variant is created, and may the user choose it immediately?
2. **Nested families:** are nested-family changes required for the first usable release or may they follow parameters/types/formulas?
3. **Installer format:** prefer the simplest offline installation. Decide between a lightweight per-user Revit add-in package and MSI after the first end-to-end build. Admin-free installation is desirable but not mandatory.
4. **Revit 2021 builds:** which exact Revit 2021 update/patch levels must be supported?
5. **History repair:** is a diagnostics/repair command needed for the first release, or only later?
6. **Comparison export:** is exporting a comparison report needed later?
