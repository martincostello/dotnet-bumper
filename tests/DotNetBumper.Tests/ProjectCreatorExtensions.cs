// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace Microsoft.Build.Utilities.ProjectCreation;

internal static class ProjectCreatorExtensions
{
#pragma warning disable CA1308
    public static ProjectCreator Property(this ProjectCreator project, string name, bool value)
        => project.Property(name, value.ToString().ToLowerInvariant());
#pragma warning restore CA1308
}
