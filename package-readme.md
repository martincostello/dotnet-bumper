# .NET Bumper ⛐📦⤴️✨

[![NuGet][package-badge]][package-download]
[![Build status][build-badge]][build-status]
[![codecov][coverage-badge]][coverage-report]

## Overview

_.NET Bumper_ is a .NET Global Tool to upgrade projects to a newer version of .NET.

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

## Quick Start

To install the tool and upgrade a .NET 6 or later project to the latest
[Long Term Support (LTS)][lts] version of .NET, run the following command:

```console
dotnet tool install --global MartinCostello.DotNetBumper
dotnet bumper .
```

## Usage

For a full list of options, run `dotnet bumper --help`.

## Pre-requisites

- .NET 8 must be installed to use the tool
  - The .NET SDK version to upgrade to must also be installed if this is different
- The [dotnet-outdated][dotnet-outdated] .NET Global tool must also be installed
- Any project being upgraded must already target at least .NET 6

## Feedback

Any feedback or issues for this tool can be added to the issues in [GitHub][issues].

## License

This project is licensed under the [Apache 2.0][license] license.

[build-badge]: https://github.com/martincostello/dotnet-bumper/actions/workflows/build.yml/badge.svg?branch=main&event=push
[build-status]: https://github.com/martincostello/dotnet-bumper/actions?query=workflow%3Abuild+branch%3Amain+event%3Apush "Continuous Integration for this project"
[coverage-badge]: https://codecov.io/gh/martincostello/dotnet-bumper/branch/main/graph/badge.svg
[coverage-report]: https://codecov.io/gh/martincostello/dotnet-bumper "Code coverage report for this project"
[dotnet-outdated]: https://github.com/dotnet-outdated/dotnet-outdated "dotnet-outdated"
[issues]: https://github.com/martincostello/dotnet-bumper/issues "Issues for this project on GitHub.com"
[license]: https://www.apache.org/licenses/LICENSE-2.0.txt "The Apache 2.0 license"
[lts]: https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core ".NET and .NET Core Support Policy"
[package-badge]: https://buildstats.info/nuget/MartinCostello.DotNetBumper?includePreReleases=true
[package-download]: https://www.nuget.org/packages/MartinCostello.DotNetBumper "Download dotnet-bumper from NuGet"
