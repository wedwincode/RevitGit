# Family History

Offline version history for Autodesk Revit family development.

## Status

Milestone 2 development: the minimal Revit 2021 add-in skeleton is available.

Target: Autodesk Revit 2021 on Windows.

## Problem

Family authors often preserve intermediate work by creating manually named copies:

```text
Door.rfa
Door_01.rfa
Door_02.rfa
Door_final.rfa
Door_final_new.rfa
```

This makes directories noisy and gives users no structured history or meaningful comparison between versions.

Family History keeps one working family file and manages previous states internally.

## Product principles

- fully offline at runtime;
- simple language for non-programmers;
- Git may be used internally but is hidden from normal UI;
- safe history: normal actions should not destroy prior work;
- meaningful Revit-aware comparison instead of binary diff;
- no merge/rebase/remote workflow in MVP;
- core behavior must be testable without launching Revit.

## MVP features

- initialize history for a family;
- save versions with optional notes;
- view a graphical version/variant history;
- compare two versions;
- human-readable parameter, formula, family type, and value changes;
- restore an old state as a new version;
- create a new variant from a historical version;
- switch variants;
- local/offline persistence across Revit restarts;
- hidden `.familyhistory` storage beside the working family.

## User terminology

The normal UI should use:

- Version
- History
- Variant
- Compare
- Restore
- Create variant from this version

Do not expose Git terms such as commit, SHA, HEAD, checkout, reset, merge, or rebase in the normal user experience.

## Technology

- C#
- .NET Framework 4.8 for the Revit 2021 add-in host
- WPF
- Autodesk Revit 2021 API
- embedded Git implementation, preferably LibGit2Sharp after compatibility validation
- no runtime dependency on Git for Windows

## Fixed MVP behavior

- `Save version` saves the active Revit family automatically.
- Version notes are optional; author identity is not stored.
- Restoring an old state creates a new version and never rewrites history.
- A user-facing `Variant` is an internal branch; v1 only supports creating one from an old version and switching between variants.
- Detailed geometry diff is not required; `Geometry changed` is sufficient initially.
- Complete `.rfa` states are stored without application-level binary delta optimization.
- V1 is single-user, local-only, Revit 2021, and may be Russian-only.

## Repository layout

See `architecture.md` for the proposed solution structure.

## Development flow

The default development loop is test-first:

```text
write failing test
-> prove expected failure
-> implement smallest change
-> make test pass
-> refactor
-> run broader core tests
```

The majority of tests must not require Revit.

## Developer commands

The solution uses classic .NET Framework 4.8 projects. Build with the MSBuild configured in Rider or Visual Studio 2022; do not use `dotnet build`.

```powershell
# Build Debug (from a Visual Studio 2022 Developer PowerShell)
msbuild RevitGit.sln /t:Restore,Build /p:Configuration=Debug

# Test all headless projects (or use Rider: Run All Tests in Solution)
vstest.console.exe `
  tests\RevitGit.Domain.Tests\bin\Debug\RevitGit.Domain.Tests.dll `
  tests\RevitGit.Application.Tests\bin\Debug\RevitGit.Application.Tests.dll `
  tests\RevitGit.Infrastructure.FileSystem.Tests\bin\Debug\RevitGit.Infrastructure.FileSystem.Tests.dll `
  tests\RevitGit.Infrastructure.Git.Tests\bin\Debug\RevitGit.Infrastructure.Git.Tests.dll `
  tests\RevitGit.Harness.Tests\bin\Debug\RevitGit.Harness.Tests.dll `
  /Platform:x64

# Run the headless Harness
tools\RevitGit.Harness\bin\Debug\RevitGit.Harness.exe help
tools\RevitGit.Harness\bin\Debug\RevitGit.Harness.exe scenario branching
tools\RevitGit.Harness\bin\Debug\RevitGit.Harness.exe scenario branching --keep
tools\RevitGit.Harness\bin\Debug\RevitGit.Harness.exe validate C:\Work\Door.rfa

# Fast Harness validation
powershell -ExecutionPolicy Bypass -File scripts\run-harness-smoke.ps1 -Configuration Debug
```

Run tests through Rider's test runner. `RevitGit.Harness.Tests` covers CLI behavior; the Domain, Application, FileSystem, and Git test projects cover the core layers. Failed scenarios always retain their isolated workspace. Successful scenarios remove it unless `--keep` is supplied.

Current transition state: `history.json` remains the canonical application history representation. The Git graph is synchronized backing topology and is validated against `history.json`; the Harness detects divergence but never repairs it. The legacy `versions/` directory remains in the filesystem layout, while the Git adapter stores version content only in Git.

## Build modes

Two build paths are expected:

1. Core build/test — no Revit installation needed.
2. Revit 2021 build/smoke test — requires Revit 2021 API assemblies from a local installation.

Autodesk DLLs must not be committed to this repository.

## Revit 2021 development prerequisites

`RevitGit.Revit2021` targets .NET Framework 4.8/x64 and compiles against `RevitAPI.dll` and `RevitAPIUI.dll` from a local Autodesk Revit 2021 installation. The solution configurations remain `Any CPU`; only the Revit-hosted project sets `PlatformTarget=x64`. These Autodesk assemblies are build-time references only: they are not copied to the output or deployed with the add-in.

### Standard installation

When no override is supplied, the Revit project uses this fallback:

```text
C:\Program Files\Autodesk\Revit 2021
```

### Non-standard installation

Set the MSBuild property explicitly when Revit is installed elsewhere:

```powershell
msbuild src\RevitGit.Revit2021\RevitGit.Revit2021.csproj `
    /t:Restore,Build `
    /p:Configuration=Debug `
    /p:Revit2021InstallDir="D:\Autodesk\Revit 2021"
```

Alternatively, define the environment variable `Revit2021InstallDir`, set the property in Rider's MSBuild settings, or create the ignored developer-local file `Directory.Build.props.user`:

```xml
<Project>
  <PropertyGroup>
    <Revit2021InstallDir>D:\Autodesk\Revit 2021</Revit2021InstallDir>
  </PropertyGroup>
</Project>
```

An explicit `/p:Revit2021InstallDir=...` global property has priority. If the resolved directory does not contain `RevitAPI.dll`, `RevitAPIUI.dll`, and `Revit.exe`, the Revit project fails with a message that includes the resolved path. Headless projects and tests do not require Revit.

### Build

Use Rider's build action or Visual Studio 2022 MSBuild. The ordinary build never deploys the add-in and never modifies `%APPDATA%`.

### Deploy

Close Revit, build the desired configuration, and deploy explicitly:

```powershell
.\scripts\deploy-revit2021.ps1 -Configuration Debug
```

The default destination is `%APPDATA%\Autodesk\Revit\Addins\2021`. A safe custom destination is supported for verification:

```powershell
.\scripts\deploy-revit2021.ps1 `
    -Configuration Debug `
    -Destination C:\Temp\RevitAddinsTest
```

The deployment contains only `RevitGit.addin` and `RevitGit\RevitGit.Revit2021.dll`. It does not contain Autodesk API assemblies or Milestone 1 Git/native dependencies.

### Remove

```powershell
.\scripts\remove-revit2021.ps1
```

The script removes only the `RevitGit.addin` manifest and `RevitGit` plugin directory. It also accepts `-Destination` for a custom location.

### Start/debug Revit

For a Rider Run/Debug configuration set the executable to:

```text
<Revit2021InstallDir>\Revit.exe
```

Do not commit machine-specific `.idea` run configurations. After deploying, Revit discovers `RevitGit.addin` during startup; open the `История семейств` tab and choose `Проверка`. Revit must be closed before rebuilding or replacing a loaded DLL. There is no hot reload.

The optional helper uses an explicit parameter, environment/local override, then the standard fallback:

```powershell
.\scripts\start-revit2021.ps1 -Revit2021InstallDir "D:\Autodesk\Revit 2021"
```

### Important

`Revit2021InstallDir` is a developer build/debug setting, not an end-user plugin setting. Deployment always targets the Revit per-user Addins directory regardless of the drive containing `Revit.exe`. At runtime the add-in neither resolves the Revit installation nor searches for or copies Revit API assemblies; the Revit host supplies them.

## Documentation

- `project.md` — product behavior, scope, terminology, acceptance criteria, open decisions.
- `architecture.md` — technical boundaries, solution layout, Revit/Git adapters, testing strategy.
- `AGENTS.md` — instructions for Codex/other coding agents.
- `.gitignore` — repository ignore rules.

## Initial implementation order

1. Create solution/project skeleton.
2. Implement Domain models and graph semantics with tests.
3. Implement pure snapshot diff engine with fixtures and golden tests.
4. Implement Git-backed history store with temporary-directory integration tests.
5. Implement headless harness scenarios.
6. Build Revit 2021 adapter for basic family metadata/parameters/types/formulas.
7. Add WPF ViewModels and UI shell.
8. Wire Revit dockable pane and ExternalEvent request bridge.
9. Add restore/version/variant workflows end-to-end.
10. Package and test simple offline installation for Revit 2021.

## Non-goals

Do not add merge, rebase, remotes, GitHub integration, cloud sync, or semantic RFA merge unless the project specification is explicitly changed.
