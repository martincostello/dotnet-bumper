# .NET Bumper ⛐📦⤴️

> [!NOTE]
> This project is currently experimental.

<!--
[![NuGet][package-badge]][package-download]
-->

[![Build status][build-badge]][build-status]
[![codecov][coverage-badge]][coverage-report]
[![OpenSSF Scorecard][scorecard-badge]][scorecard-report]

## Introduction

A .NET Global Tool to upgrade projects to a newer version of .NET.

> [!NOTE]
> Upgrades are made on a best-effort basis. You should always review the
> changes made by the tool and test any changes made to validate the upgrade.

## Building and Testing

Compiling the application yourself requires Git and the [.NET SDK][dotnet-sdk] to be installed.

To build and test the application locally from a terminal/command-line, run the following set of commands:

```powershell
git clone https://github.com/martincostello/dotnet-bumper.git
cd dotnet-bumper
./build.ps1
```

## Feedback

Any feedback or issues can be added to the issues for this project in [GitHub][issues].

## Repository

The repository is hosted in [GitHub][repo]: <https://github.com/martincostello/dotnet-bumper.git>

## License

This project is licensed under the [Apache 2.0][license] license.

[build-badge]: https://github.com/martincostello/dotnet-bumper/workflows/build/badge.svg?branch=main&event=push
[build-status]: https://github.com/martincostello/dotnet-bumper/actions?query=workflow%3Abuild+branch%3Amain+event%3Apush "Continuous Integration for this project"
[coverage-badge]: https://codecov.io/gh/martincostello/dotnet-bumper/branch/main/graph/badge.svg
[coverage-report]: https://codecov.io/gh/martincostello/dotnet-bumper "Code coverage report for this project"
[dotnet-sdk]: https://dotnet.microsoft.com/download "Download the .NET SDK"
[issues]: https://github.com/martincostello/dotnet-bumper/issues "Issues for this project on GitHub.com"
[license]: https://www.apache.org/licenses/LICENSE-2.0.txt "The Apache 2.0 license"
<!--
[package-badge]: https://buildstats.info/nuget/MartinCostello.DotNetBumper?includePreReleases=true
[package-download]: https://www.nuget.org/packages/MartinCostello.DotNetBumper "Download dotnet-bumper from NuGet"
-->
[repo]: https://github.com/martincostello/dotnet-bumper "This project on GitHub.com"
[scorecard-badge]: https://api.securityscorecards.dev/projects/github.com/martincostello/dotnet-bumper/badge
[scorecard-report]: https://securityscorecards.dev/viewer/?uri=github.com/martincostello/dotnet-bumper "OpenSSF Scorecard for this project"
