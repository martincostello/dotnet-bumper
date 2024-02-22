// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.DotNetBumper;

public sealed class LambdaRuntimeTestData : TheoryData<string>
{
    public LambdaRuntimeTestData()
    {
        string[] unsupportedRuntimes =
        [
            "aaa",
            "dotnetcore1.0",
            "dotnetcore2.0",
            "dotnetcore2.1",
            "dotnetcore3.1",
            "dotnet5.0",
            "go1.x",
            "java8",
            "java8.al2",
            "java11",
            "java17",
            "java21",
            "nodejs",
            "nodejs4.3",
            "nodejs4.3-edge",
            "nodejs6.10",
            "nodejs8.10",
            "nodejs10.x",
            "nodejs12.x",
            "nodejs14.x",
            "nodejs16.x",
            "nodejs18.x",
            "nodejs20.x",
            "provided",
            "provided.al2",
            "provided.al2023",
            "python2.7",
            "python3.6",
            "python3.7",
            "python3.8",
            "python3.9",
            "python3.10",
            "python3.11",
            "python3.12",
            "ruby2.5",
            "ruby2.7",
            "ruby3.2",
        ];

        AddRange(unsupportedRuntimes);
    }
}
