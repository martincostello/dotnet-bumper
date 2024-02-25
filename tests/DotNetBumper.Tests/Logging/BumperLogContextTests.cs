// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.DotNetBumper.Logging;

public static class BumperLogContextTests
{
    [Fact]
    public static void BumperLogContext_Add_Merges_Results()
    {
        // Arrange
        var first = new DotNetResult(true, 0, string.Empty, string.Empty)
        {
            BuildLogs = new()
            {
                Entries =
                {
                    new() { Type = "Warning", Id = "1", Message = "Warning 1" },
                    new() { Type = "Error", Id = "2", Message = "Error 2" },
                },
                Summary = new Dictionary<string, IDictionary<string, long>>()
                {
                    ["Error"] = new Dictionary<string, long>() { ["2"] = 1 },
                    ["Warning"] = new Dictionary<string, long>() { ["1"] = 1 },
                },
            },
            TestLogs = new()
            {
                Outcomes = new Dictionary<string, IList<BumperTestLogEntry>>()
                {
                    ["Container1"] =
                    [
                        new() { Id = "Test1", Outcome = "Passed" },
                        new() { Id = "Test2", Outcome = "Failed" },
                        new() { Id = "Test3", Outcome = "Passed" },
                        new() { Id = "Test4", Outcome = "Passed" },
                        new() { Id = "Test5", Outcome = "Failed" },
                        new() { Id = "Test6", Outcome = "Skipped" },
                    ],
                    ["Container2"] =
                    [
                        new() { Id = "Test1", Outcome = "Passed" },
                        new() { Id = "Test2", Outcome = "Passed" },
                        new() { Id = "Test3", Outcome = "Passed" },
                        new() { Id = "Test4", Outcome = "Passed" },
                    ],
                },
                Summary = new Dictionary<string, IDictionary<string, long>>()
                {
                    ["Container1"] = new Dictionary<string, long>()
                    {
                        ["Passed"] = 3,
                        ["Failed"] = 2,
                        ["Skipped"] = 1,
                    },
                    ["Container2"] = new Dictionary<string, long>()
                    {
                        ["Passed"] = 4,
                    },
                },
            },
        };

        var second = new DotNetResult(true, 0, string.Empty, string.Empty)
        {
            BuildLogs = new()
            {
                Entries =
                {
                    new() { Type = "Warning", Id = "1", Message = "Warning 1" },
                    new() { Type = "Warning", Id = "1", Message = "Warning 1" },
                    new() { Type = "Error", Id = "3", Message = "Error 3" },
                },
                Summary = new Dictionary<string, IDictionary<string, long>>()
                {
                    ["Error"] = new Dictionary<string, long>() { ["3"] = 1 },
                    ["Warning"] = new Dictionary<string, long>() { ["1"] = 2 },
                },
            },
            TestLogs = new()
            {
                Outcomes = new Dictionary<string, IList<BumperTestLogEntry>>()
                {
                    ["Container3"] =
                    [
                        new() { Id = "Test1", Outcome = "Passed" },
                        new() { Id = "Test2", Outcome = "Passed" },
                        new() { Id = "Test3", Outcome = "Passed" },
                        new() { Id = "Test4", Outcome = "Passed" },
                        new() { Id = "Test5", Outcome = "Passed" },
                    ],
                },
                Summary = new Dictionary<string, IDictionary<string, long>>()
                {
                    ["Container3"] = new Dictionary<string, long>()
                    {
                        ["Passed"] = 5,
                    },
                },
            },
        };

        var context = new BumperLogContext();

        // Act
        context.Add(first);
        context.Add(second);

        // Assert
        context.BuildLogs.ShouldNotBeNull();

        context.BuildLogs.Entries.ShouldNotBeNull();
        context.BuildLogs.Entries.Count.ShouldBe(5);

        context.BuildLogs.Summary.ShouldNotBeNull();
        context.BuildLogs.Summary.Count.ShouldBe(2);

        context.BuildLogs.Summary.ShouldContainKey("Error");
        context.BuildLogs.Summary["Error"].ShouldSatisfyAllConditions(
            (p) => p.Count.ShouldBe(2),
            (p) => p.ShouldContainKeyAndValue("2", 1),
            (p) => p.ShouldContainKeyAndValue("3", 1));

        context.BuildLogs.Summary.ShouldContainKey("Warning");
        context.BuildLogs.Summary["Warning"].ShouldSatisfyAllConditions(
            (p) => p.Count.ShouldBe(1),
            (p) => p.ShouldContainKeyAndValue("1", 3));

        context.TestLogs.ShouldNotBeNull();

        context.TestLogs.Outcomes.ShouldNotBeNull();
        context.TestLogs.Outcomes.Count.ShouldBe(3);

        context.TestLogs.Outcomes.ShouldContainKey("Container1");
        context.TestLogs.Outcomes["Container1"].ShouldSatisfyAllConditions(
            (p) => p.Count.ShouldBe(6),
            (p) => p.Count(x => x.Outcome == "Passed").ShouldBe(3),
            (p) => p.Count(x => x.Outcome == "Failed").ShouldBe(2),
            (p) => p.Count(x => x.Outcome == "Skipped").ShouldBe(1));

        context.TestLogs.Outcomes.ShouldContainKey("Container2");
        context.TestLogs.Outcomes["Container2"].ShouldSatisfyAllConditions(
            (p) => p.Count.ShouldBe(4),
            (p) => p.Count(x => x.Outcome == "Passed").ShouldBe(4));

        context.TestLogs.Outcomes.ShouldContainKey("Container3");
        context.TestLogs.Outcomes["Container3"].ShouldSatisfyAllConditions(
            (p) => p.Count.ShouldBe(5),
            (p) => p.Count(x => x.Outcome == "Passed").ShouldBe(5));

        context.TestLogs.Summary.ShouldNotBeNull();
        context.TestLogs.Summary.Count.ShouldBe(3);

        context.TestLogs.Summary.ShouldContainKey("Container1");
        context.TestLogs.Summary["Container1"].ShouldSatisfyAllConditions(
            (p) => p.Count.ShouldBe(3),
            (p) => p.ShouldContainKeyAndValue("Passed", 3),
            (p) => p.ShouldContainKeyAndValue("Failed", 2),
            (p) => p.ShouldContainKeyAndValue("Skipped", 1));

        context.TestLogs.Summary.ShouldContainKey("Container2");
        context.TestLogs.Summary["Container2"].ShouldSatisfyAllConditions(
            (p) => p.Count.ShouldBe(1),
            (p) => p.ShouldContainKeyAndValue("Passed", 4));

        context.TestLogs.Summary.ShouldContainKey("Container3");
        context.TestLogs.Summary["Container3"].ShouldSatisfyAllConditions(
            (p) => p.Count.ShouldBe(1),
            (p) => p.ShouldContainKeyAndValue("Passed", 5));
    }
}
