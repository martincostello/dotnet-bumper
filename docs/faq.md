# FAQs

## How does .NET Bumper work?

_.NET Bumper_ is a .NET Global tool that helps automate the process of upgrading a project to a newer
version of .NET. This is achieved by searching your project for well-known files (`global.json`, `*.csproj`, `Dockerfile`, etc.)
for known content that is likely to be needed to be updated when moving to a new version of .NET.

Bumper then applies edits it deems that are neccessary to move you towards the specified version of .NET.
For example, it might update the version of the .NET SDK in a `global.json` file, or update the
`TargetFramework` property in a `.csproj` file.

The versions of .NET that can be upgraded to are determined from the [.NET release notes][dotnet-release-notes]
in GitHub.

## What is the difference between .NET Bumper and the .NET Upgrade Assistant?

The philosophy behind .NET Bumper is that Bumper is intended to make it easier for you to make
_incremental_ updates from one major version of .NET to the next in small steps for each annual
release of .NET. It also supports moving from Long Term Support (LTS) release to another if you
do not upgrade your version of .NET annually.

In essence, .NET Bumper's purpose is to do the boring incremental changes so that a simple version
upgrade can be done with minimal effort and to help keep your projects evergreen.

What .NET Bumper is _not_ intended for is to adopt major new framework and language features en masse,
such as using new C# features, moving from MVC Controllers to Minimal Actions, or moving from
.NET Framework to .NET (Core).

Such larger scale changes are left to tools such as the [_.NET Upgrade Assistant_][upgrade-assistant]
which is a Microsoft tool that is designed to help you do these sorts of large-scale software upgrades.

Another notable difference between the two tools is that .NET Bumper is open source. Contributes to
the tool to add new features and fix bugs are welcome. Check out the [contributing guide][contributions]
for more information.

## Why is my build broken/tests failing?

.NET Bumper will build and test your project using `dotnet test` after the upgrade to produce a report on the
success of the upgrade, but due to various factors, the build and/or tests may fail.

Reasons for this could include:

- New code analysis options are introduced in the new version of .NET that are creating new warnings/errors;
- A behavioural change in .NET is causing one or more tests to fail;
- The test coverage has changed and the test runs are failing as a result;
- Some of your tests require custom manual setup (e.g. end-to-end tests) and cannot be run by .NET Bumper;
- Conflicting NuGet package versions;
- Syntax changes in a newer version of C#.

## Why haven't all my NuGet packages been updated?

.NET Bumper will only updates specific packages that are known to require an upgrade when moving to a new version
of .NET.

If you have other packages that create version conflicts that need to be upgraded, you must update these on
the created branch manually.

If you prefer, you can instead configure a custom set of NuGet package references to be included and excluded in updates by the tool.

To do this, add either a JSON or YAML configuration file to the root of your repository with the name
`.dotnet-bumper.json` or `.dotnet-bumper.yml` respectively. A custom configuration file can also be specified
using the `--configuration-file` option. The JSON schema for the configuration file can be found [here][dotnet-bumper-schema].

Example content for these files is shown below.

```json
{
  "$schema": "https://raw.githubusercontent.com/martincostello/dotnet-bumper/main/dotnet-bumper-schema.json",
  "excludeNuGetPackages": [
    "Package.That.Should.Not.Be.Updated"
  ],
  "includeNuGetPackages": [
    "An.Extra.Package.That.Needs.Updating"
  ],
  "noWarn": [
    "CUSTOM123"
  ],
  "remainingReferencesIgnore": [
    "things-to-ignore/*",
    "ignore-me.txt"
  ]
}
```

```yml
excludeNuGetPackages:
  - Package.That.Should.Not.Be.Updated
includeNuGetPackages:
  - An.Extra.Package.That.Needs.Updating
noWarn:
  - CUSTOM123
remainingReferencesIgnore:
  - "things-to-ignore/*"
  - "ignore-me.txt"
```

Ultimately, .NET packages are updated by the tool via use of the [.NET Outdated][dotnet-outdated] tool - if your packages
are still not being updated, it may be an issue with .NET Outdated itself, rather than with .NET Bumper.

## What are the Remaining References?

.NET Bumper is not able to update all references to .NET in a project that may need to be upgraded. This could
be due to a lack of confidence in the tool to do so with a high degree of accuracy, or because such an update
is technically difficult to do (e.g. updating a PowerShell script).

The tool can however search files to look for references that _might_ need updating and draw your attention to them.

Any list of remaining references should be reviewed and updated manually as necessary.

## Why are comments are being stripped from my `json` files?

This is a known issue with the tool, see [martincostello/dotnet-bumper#46][dotnet-bumper-46].

## Does the tool support upgrading to .NET vNext?

Yes! You can use .NET Bumper to upgrade to a forthcoming version of .NET while it is in preview too.

To do so, the tool needs to be run with the `--upgrade-type` option set to `Preview`.

## Can I run the tool in bulk for many repositories?

.NET Bumper was designed with the intention of being run on a per-repository basis.

For my own personal usage, I created a GitHub Actions workflow that allows me to run the tool against all of
the repositories that I maintain. The workflow can be found [here][dotnet-bumper-workflow] - you're welcome to
use it as a source of inspiration to create your own bulk upgrade processes.

## Why didn't .NET Bumper update this file that needs updating?

.NET Bumper currently supports the use cases that I have come across in my own projects and those in the projects
I help maintain in my day-to-day work. Because of this, other valid use cases may exist, but I'm not aware of them
of I've not needed them, so they are not currently supported.

If a type of file edit that you need is not currently supported, please raise an issue on the GitHub repository
suggesting a feature request for the tool.

## Why has .NET Bumper broken all my multi-targeting NuGet package versions?

.NET Bumper uses [.NET Outdated][dotnet-outdated] to update NuGet package versions. This tool, which itself is
built on top of the NuGet client, is not able to fully understand the intricacies and complexities of multi-targeting.

If you have custom MSBuild properties configured for multi-targeting, .NET Bumper will not be able to update these
for you in a correct manner. This is a known limitation.

In these scenarios, you should use whatever existing process you have in place to update your package versions
as appropriate to fulfill your multi-targeting requirements. [Contributions][contributions] are even better!

Only contributions to "industry standard" files related to the .NET eco-system will be considered for inclusion.
Contributions for custom updates for files/tooling you may have in your own private projects will not be accepted for inclusion.

By _"industry standard"_ files, I mean content such as Azure/AWS tooling, Dockerfiles, Serverless, .NET project files, etc.

[contributions]: ../.github/CONTRIBUTING.md
[dotnet-bumper-46]: https://github.com/martincostello/dotnet-bumper/issues/46
[dotnet-bumper-workflow]: https://github.com/martincostello/github-automation/blob/main/.github/workflows/dotnet-bumper.yml
[dotnet-bumper-schema]: ../dotnet-bumper-schema.json
[dotnet-outdated]: https://github.com/dotnet-outdated/dotnet-outdated
[dotnet-release-notes]: https://github.com/dotnet/core/tree/main/release-notes#readme
[upgrade-assistant]: https://dotnet.microsoft.com/platform/upgrade-assistant
