# Coding Agent Instructions

This file provides guidance to coding agents when working with code in this repository.

## Project Overview

_.NET Bumper_ is a .NET Global Tool that automates upgrading .NET projects to a newer version of .NET. It updates `global.json`, target frameworks, NuGet packages, Dockerfiles, GitHub Actions workflows, AWS Lambda config, and more.

## Build, Test, and Pack

```powershell
# Full build + test + pack (primary workflow)
./build.ps1

# Build only
dotnet build

# Run all tests
dotnet test --configuration Release

# Run a single test class
dotnet test --configuration Release --filter "FullyQualifiedName~TargetFrameworkUpgraderTests"

# Run a single test method
dotnet test --configuration Release --filter "FullyQualifiedName~TargetFrameworkUpgraderTests.UpgradeAsync_Upgrades_Properties"

# Pack without running tests
./build.ps1 -SkipTests
```

Build output goes to `./artifacts/` (configured via `UseArtifactsOutput`). `MSBuildTreatWarningsAsErrors` is enabled globally — there must be zero warnings.

The `lint.yml` CI workflow runs additional checks. The core PSScriptAnalyzer step can be reproduced locally via:

```powershell
pwsh -NoLogo -NoProfile -Command "& {
  $settings = @{ IncludeDefaultRules = $true }
  $issues = Invoke-ScriptAnalyzer -Path . -Recurse -Settings $settings -ReportSummary -Severity Warning,Error
  if ($issues.Count -gt 0) { exit 1 }
}"
```

See `.github/workflows/lint.yml` for the full set of linters and options used in CI.

## Solution Structure

The main CLI tool multi-targets all currently supported versions of .NET. Tests run on the latest supported version of .NET only.

- **`src/DotNetBumper`** — CLI entry point; thin wrapper that wires up DI and calls `ProjectUpgrader`
- **`src/DotNetBumper.Core`** — Core library; all upgrade logic lives here
- **`src/DotNetBumper.MSBuild`** — MSBuild logger that captures build log data during upgrades
- **`src/DotNetBumper.TestLogger`** — VSTest logger that captures test result data during post-processing
- **`tests/DotNetBumper.Tests`** — xUnit v3 test project covering all of the above

## Architecture

`ProjectUpgrader` is the top-level orchestrator:

1. Calls `DotNetUpgradeFinder` to resolve the target .NET version (via `releases-index.json` from the dotnet/core GitHub repo, and `aka.ms/dotnet/.../sdk-productVersion.txt` for daily builds)
2. Runs each `IUpgrader` implementation in a defined order (global.json → NuGet config → TFM upgraders → packages → code formatter)
3. On success, runs each `IPostProcessor` implementation (runs `dotnet test`, checks for leftover old-version references)

### Upgrader hierarchy

```
IUpgrader
└── Upgrader (abstract) — wraps output in a Spectre.Console status spinner
    └── FileUpgrader (abstract) — discovers files via glob patterns, calls UpgradeCoreAsync per file list
        ├── XmlFileUpgrader (abstract) — loads/saves XML with BOM preservation
        │   └── TargetFrameworkUpgrader, VisualStudioComponentUpgrader, ...
        └── (direct) DockerfileUpgrader, GitHubActionsUpgrader, PackageVersionUpgrader, ...
```

### Adding a new upgrader

1. Create a class in `src/DotNetBumper.Core/Upgraders/` inheriting from `FileUpgrader` (or `Upgrader` for non-file-based work)
2. Override `Action`, `InitialStatus`, `Patterns`, and `UpgradeCoreAsync`
3. Register it in `ServiceCollectionExtensions.AddUpgraders()`
4. Add a corresponding test class in `tests/DotNetBumper.Tests/Upgraders/`

### Adding a new post-processor

Same pattern as upgraders, but implement `IPostProcessor`, inherit from `PostProcessor`, and register in `ServiceCollectionExtensions.AddPostProcessors()`.

## Testing Conventions

- Test framework: **xUnit v3** with `Shouldly` for assertions and `NSubstitute` for mocks
- `UpgraderFixture` is the standard test helper — exposes `IAnsiConsole`, `IEnvironment` (NSubstitute mock), and `Project` (temp directory) as properties, and provides helper methods such as `CreateOptions()` and `CreateLogger<T>()` to obtain `IOptions<UpgradeOptions>` and `ILogger<T>`
- The `Project` class manages a temporary directory; use `fixture.Project.AddFileAsync(path, content)` to seed files
- HTTP calls are intercepted using `JustEat.HttpClientInterception`
- Test pattern: Arrange via `UpgraderFixture`, instantiate the target with `CreateTarget(fixture)`, call `UpgradeAsync(upgrade, fixture.CancellationToken)`, assert on `ProcessingResult` and file contents
- Tests run idempotency: call `UpgradeAsync` a second time and assert it returns `ProcessingResult.None`
- Logging via `MartinCostello.Logging.XUnit.v3` — pass `ITestOutputHelper` to `UpgraderFixture`
- End-to-end tests in `EndToEndTests.cs` exercise the full upgrade pipeline against real project structures

## Key Types

| Type | Purpose |
| ---- | ------- |
| `UpgradeInfo` | Target .NET channel, SDK version, release type, support phase |
| `ProcessingResult` | Enum: `None`, `Success`, `Warning`, `Error` — upgraders return their "worst" result via `.Max()` |
| `BumperLogContext` | Collects changelog entries and timing for output log files |
| `UpgradeOptions` | CLI/config options: `ProjectPath`, `UpgradeType`, `TreatWarningsAsErrors`, `LogFormat` |
| `WellKnownFileNames` | Constants for file patterns (e.g. `*.csproj`, `global.json`) |
| `IEnvironment` | Abstraction over environment variables; always mock this in tests via NSubstitute |

## Code Style

- `.cs` files use **UTF-8 BOM** encoding — preserve this when writing or modifying source files
- Line endings are **CRLF** throughout
- All C# source files (`.cs`) begin with the standard file header (copyright and Apache 2.0 license) configured via `.editorconfig`
- StyleCop.Analyzers is applied globally (see `stylecop.json` for config)
- Logging uses `[LoggerMessage]` source-generated partial methods inside a nested `private static partial class Log` decorated with `[ExcludeFromCodeCoverage]`
- Central Package Management via `Directory.Packages.props` — never add a `Version` attribute to `<PackageReference>` in project files; only add `<PackageVersion>` entries to `Directory.Packages.props`
- `ManagePackageVersionsCentrally` is enabled — use `<PackageReference Include="..." />` without version in `.csproj` files

## CI

The `build.yml` workflow runs on macOS, Linux, and Windows. All three must pass. The `lint.yml` workflow runs separate linting checks. GitHub Actions workflow steps use pinned SHA commit hashes for third-party actions.

## General guidelines

- Always ensure code compiles with no warnings or errors and tests pass locally before pushing changes.
- Do not use APIs marked with `[Obsolete]`.
- Bug fixes should **always** include a test that would fail without the corresponding fix.
- Do not introduce new dependencies unless specifically requested.
- Do not update existing dependencies unless specifically requested.
