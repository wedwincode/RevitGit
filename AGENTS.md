# AGENTS.md

## Mission

Build Family History, an offline Autodesk Revit 2021 add-in that gives non-programmer family authors safe, understandable version history and semantic comparisons without exposing Git concepts.

Read `project.md` and `architecture.md` before making architectural or user-facing changes.

## Primary constraints

1. Revit 2021 add-in code targets .NET Framework 4.8.
2. Runtime must work without internet access.
3. Runtime must not require a separately installed `git.exe`.
4. Normal UI must not expose Git terminology.
5. Merge, rebase, remotes, pull requests, and semantic merge are out of scope.
6. Core logic must remain testable without Revit.
7. Only the Revit adapter may reference `Autodesk.Revit.*` assemblies.
8. Never commit Autodesk Revit DLLs to this repository.
9. Never silently discard reachable history.
10. Prefer simple, deterministic implementations over speculative abstraction.

# Tool usage

When inspecting, searching, or reading project files, prefer Rider MCP tools over shell commands.

Use Rider MCP tools such as read_file, search_text, search_file, search_symbol, get_file_problems, and other IDE-native tools whenever they can perform the operation.

Do not use PowerShell, Get-Content, rg, findstr, cat, grep, or similar terminal commands merely to inspect or search project source files.

Use terminal commands only when the required operation is not available through Rider MCP or when command-line execution is inherently required.

# .NET Framework build

This solution targets .NET Framework 4.8.

Do not use `dotnet build` or `dotnet run` for classic .NET Framework projects.

Prefer Rider MCP build tools such as `build_solution_start` and `build_solution_state`.

If command-line MSBuild is required, use the MSBuild installation configured in Rider or a Visual Studio 2022 / Build Tools MSBuild.exe. Do not search for arbitrary Visual Studio executables unless necessary.

## Required development loop

For behavior changes, use red-green-refactor unless the change is purely mechanical/documentation.

1. Locate the smallest relevant test project.
2. Write or modify a test that expresses the intended behavior.
3. Run that test and confirm it fails for the expected reason.
4. Make the smallest production-code change that should satisfy it.
5. Run the focused test until green.
6. Refactor if needed.
7. Run the affected project suite.
8. Run `build/test-core.ps1` before considering the task complete when available.

For changes affecting history, variants, restore, storage, or semantic diff, run the relevant `RevitGit.Harness` scenario after tests. Prefer this headless verification before Revit-specific work whenever the behavior can be reproduced without Autodesk Revit.

Do not write production behavior first and add a vacuous test afterward.

If a behavior cannot reasonably be unit-tested because it is at the Autodesk boundary, isolate it behind an interface and test everything around it. Add or update a Revit smoke scenario for the adapter behavior.

## Test quality rules

Tests must verify externally observable behavior, not private implementation details.

Prefer:

- deterministic fixtures;
- temporary directories;
- explicit clocks/identities;
- value equality;
- golden snapshot files where serialization format matters.

Avoid:

- sleeps;
- dependence on test execution order;
- global machine Git configuration;
- real user repositories;
- network access;
- tests that require Revit for pure domain/application behavior.

Every bug fix should get a regression test when practical.

# Test execution

This solution targets .NET Framework 4.8.

Prefer Rider MCP and Rider test infrastructure for discovering and running tests.

Use Rider tools such as findTests and execute_run_configuration when possible.

Do not search the filesystem for vstest.console.exe, testhost.exe, MSBuild.exe, or Visual Studio installation paths unless Rider MCP cannot perform the required operation.

Do not use recursive PowerShell searches under Program Files or the NuGet package cache to locate test runners.

## Architecture rules

### Domain

Must be pure.

No references to:

- Autodesk Revit;
- WPF;
- LibGit2Sharp;
- filesystem APIs where avoidable;
- system clock directly.

### Application

Contains use cases and ports.

May depend on Domain.

Must not depend on concrete Git/Revit/UI implementations.

### Infrastructure.Git

Contains LibGit2Sharp-specific code.

Expose behavior through Application interfaces.

Do not leak LibGit2Sharp types outside the adapter.

### Revit2021

Contains all Autodesk API integration.

Translate Revit objects into neutral snapshots/DTOs immediately.

Do not pass `Document`, `Element`, `FamilyManager`, `Parameter`, etc. into Domain/Application.

Do not use Revit API objects from arbitrary worker threads.

Modeless UI requests requiring Revit API access must execute through the Revit-approved request mechanism (`ExternalEvent`).

### UI

Prefer MVVM.

ViewModels should be testable without Revit/WPF window creation where practical.

UI labels use product terminology from `project.md`.

Do not display raw SHA, HEAD, refs, or Git command names in normal UI.

## History semantics

Internal implementation may use Git commits/branches, but application semantics are:

- Version
- Variant
- Current version
- Restore as new version
- Create variant from version

### Restore as new version

Must preserve all later history. Do not rewrite a branch for the normal restore action.

### Create variant

A variant may start from any existing version. V1 supports create and switch only. Do not add rename, archive, delete, or merge behavior unless the specification changes.

## Snapshot/diff rules

`FamilySnapshot` is for comparison, not reconstruction.

The `.rfa` binary is the restoration source.

Snapshot serialization must be deterministic and schema-versioned.

Diff must be computed by pure code from two snapshots.

Never present raw internal Revit units to users. Normalize values first.

Never identify a parameter only by display name when a stronger stable identity exists.

Detailed geometry diff is not required for MVP. Do not expand geometry scope without an explicit requirement.

## Git/storage rules

The end user must not need Git for Windows.

Prefer LibGit2Sharp if the compatibility spike succeeds.

All Git operations must be behind an interface.

Integration tests create isolated repositories in temporary directories and supply identity/config explicitly.

Do not implement custom binary delta encoding in MVP. Store complete `.rfa` states and optimize only after measurement.

Do not optimize repository size before measuring actual `.rfa` repository growth with representative fixture files.

## Error handling

Do not swallow exceptions silently.

At boundaries:

- convert technical exceptions into typed application failures;
- log diagnostic detail locally;
- return actionable, non-technical user messages.

Never show a stack trace in the normal UI.

File replacement/restoration must use safe temporary-file techniques and validation where practical.

## Revit development rules

Revit-specific builds may rely on a local `REVIT_2021_DIR` or equivalent MSBuild property.

Do not hard-code one developer's absolute Revit path in committed project files.

Keep `.addin` manifests generated/configurable for local development and packaging.

Revit smoke tests are separate from the default headless test loop.

For Revit-specific changes:

1. run headless tests;
2. run the Harness smoke scenarios;
3. build the Revit project using `Revit2021InstallDir`;
4. deploy explicitly;
5. run the manual Revit smoke checklist.

For changes to the Revit Save Version vertical slice:

1. run focused unit tests;
2. run all headless tests;
3. run the Harness smoke scenarios;
4. build the Revit project;
5. deploy explicitly;
6. manually create a version in a saved test `.rfa`;
7. run `RevitGit.Harness validate <path-to-rfa>` against the resulting history.

Manual Revit testing never replaces headless verification.

For Revit snapshot extraction changes:

1. run headless tests first;
2. run the Harness smoke scenarios;
3. build the Revit project with `Revit2021InstallDir`;
4. run manual extraction against a real fixture `.rfa`;
5. verify two unchanged extractions are equal and have the same geometry fingerprint;
6. verify the real `Revit -> Snapshot -> Diff` path after a controlled family change.

Do not replace real Revit snapshot smoke testing with a mocked `Document`.

## Code style

- favor small classes with explicit responsibility;
- prefer immutable domain records/value objects where supported by the chosen C# language level/toolchain;
- prefer constructor injection;
- avoid service locator/global mutable state;
- avoid static access to filesystem, clock, Revit context, or Git repositories in application logic;
- make asynchronous behavior explicit;
- add comments only when they explain a non-obvious decision or constraint.

## Dependency policy

Before adding a new package:

1. explain what capability it provides;
2. verify .NET Framework 4.8/Revit 2021 compatibility where relevant;
3. verify it can be deployed offline with the add-in;
4. prefer maintained dependencies with clear licensing;
5. avoid adding packages for trivial functionality.

Pin package versions rather than floating them.

## Completion checklist for implementation tasks

A task is not complete until applicable items are true:

- behavior is covered by a failing-then-passing test;
- focused tests pass;
- broader affected tests pass;
- core suite passes;
- no new Revit dependency leaked into core;
- no Git terminology leaked into normal UI;
- failure/recovery behavior was considered;
- docs were updated if public behavior or architecture changed.

## Do not do these without explicit instruction

- add merge/rebase/cherry-pick UI;
- add network access;
- add telemetry;
- add cloud storage;
- add automatic background commits;
- rewrite reachable history destructively or add a reset-style `Set current` action;
- commit Revit API DLLs;
- introduce a database just because one may be useful later;
- implement detailed geometry matching before parameter/type/formula diff is solid.
