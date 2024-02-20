# .NET Bumper ⛐📦⤴️✨

> [!NOTE]
> This project is currently experimental.

[![NuGet][package-badge]][package-download]

[![Build status][build-badge]][build-status]
[![codecov][coverage-badge]][coverage-report]
[![OpenSSF Scorecard][scorecard-badge]][scorecard-report]

## Overview

_.NET Bumper_ is a .NET Global Tool to upgrade projects to a newer version of .NET.

![.NET Bumper in action][demo]

Bumper helps you upgrade your .NET projects to a newer version by taking
care of some of the steps required to move from one version to another. Bumper supports
upgrading to both _Long Term Support_ (LTS) and _Standard Term Support_ (STS) versions
of .NET, _and_ the latest preview versions of .NET. 🚀

Steps the tool can perform on your behalf include:

- Updating the .NET SDK version in `global.json` 🧑‍💻
- Upgrading the Target Framework of your project files ⚙️
- Upgrading .NET, ASP.NET Core and EFCore NuGet packages to the appropriate versions 📦
- Updating image tags in Dockerfiles 🐳
- Running `dotnet test` to validate the upgrade 🧪

> [!NOTE]
> Upgrades are made on a best-effort basis. You should always review the
> changes made by the tool and test any changes made to validate the upgrade.

## Quick Start

To install the tool and upgrade a .NET 6 or later project to the latest
[Long Term Support (LTS)][lts] version of .NET, run the following command:

```console
dotnet tool install --global MartinCostello.DotNetBumper
dotnet bumper .
```

## Usage

For a full list of options, run `dotnet bumper --help`.

An example of the output is shown below.

### Examples

> Upgrade a project to the current LTS version of .NET

```console
dotnet bumper ~/projects/awesome-project
```

> Upgrade a project to the latest version of .NET

```console
dotnet bumper ~/projects/awesome-project --upgrade-type Latest
```

> Upgrade a project to the preview version of .NET

```console
dotnet bumper ~/projects/awesome-project --upgrade-type Preview
```

> Upgrade a project to a specific version of .NET

```console
dotnet bumper ~/projects/awesome-project --channel 8.0
```

> Upgrade a project to the current LTS version of .NET and
> then run `dotnet test` after the upgrade has completed

```console
dotnet bumper ~/projects/awesome-project --test
```

### Options

```console
> dotnet bumper --help
Upgrades projects to a newer version of .NET.

Usage: dotnet bumper [options] <ProjectPath>

Arguments:
  ProjectPath               The path to directory containing a .NET 6+ project to be upgraded. If not specified, the
                            current directory will be used.

Options:
  -v|--verbose              Show verbose output
  --version                 Show version information.
  -?|-h|--help              Show help information.
  -c|--channel <CHANNEL>    The .NET release channel to upgrade to in the format "major.minor".
  -t|--upgrade-type <TYPE>  The type of upgrade to perform.
                            Allowed values are: Lts, Latest, Preview.
  -test|--test              Test the upgrade by running dotnet test on completion.
  -e|--warnings-as-errors   Treat any warnings encountered during the upgrade as errors.
```

## Pre-requisites

- .NET 8 must be installed to use the tool
  - The .NET SDK version to upgrade to must also be installed if this is different
- The [dotnet-outdated][dotnet-outdated] .NET Global tool must also be installed
- Any project being upgraded must already target at least .NET 6

## Building and Testing

Compiling the application yourself requires Git and the [.NET SDK][dotnet-sdk] to be installed.

To build and test the application locally from a terminal, run the following set of commands:

```powershell
git clone https://github.com/martincostello/dotnet-bumper.git
cd dotnet-bumper
dotnet tool restore
./build.ps1
```

## Feedback

Any feedback or issues can be added to the issues for this project in [GitHub][issues].

## Repository

The repository is hosted in [GitHub][repo]: <https://github.com/martincostello/dotnet-bumper.git>

## License

This project is licensed under the [Apache 2.0][license] license.

[build-badge]: https://github.com/martincostello/dotnet-bumper/actions/workflows/build.yml/badge.svg?branch=main&event=push
[build-status]: https://github.com/martincostello/dotnet-bumper/actions?query=workflow%3Abuild+branch%3Amain+event%3Apush "Continuous Integration for this project"
[coverage-badge]: https://codecov.io/gh/martincostello/dotnet-bumper/branch/main/graph/badge.svg
[coverage-report]: https://codecov.io/gh/martincostello/dotnet-bumper "Code coverage report for this project"
[demo]: ./docs/demo.gif "A demonstration of the .NET Bumper tool"
[dotnet-outdated]: https://github.com/dotnet-outdated/dotnet-outdated "dotnet-outdated"
[dotnet-sdk]: https://dotnet.microsoft.com/download "Download the .NET SDK"
[issues]: https://github.com/martincostello/dotnet-bumper/issues "Issues for this project on GitHub.com"
[license]: https://www.apache.org/licenses/LICENSE-2.0.txt "The Apache 2.0 license"
[lts]: https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core ".NET and .NET Core Support Policy"
[package-badge]: https://buildstats.info/nuget/MartinCostello.DotNetBumper?includePreReleases=true
[package-download]: https://www.nuget.org/packages/MartinCostello.DotNetBumper "Download dotnet-bumper from NuGet"
[repo]: https://github.com/martincostello/dotnet-bumper "This project on GitHub.com"
[scorecard-badge]: https://api.securityscorecards.dev/projects/github.com/martincostello/dotnet-bumper/badge
[scorecard-report]: https://securityscorecards.dev/viewer/?uri=github.com/martincostello/dotnet-bumper "OpenSSF Scorecard for this project"
