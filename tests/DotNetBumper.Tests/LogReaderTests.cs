// Copyright (c) Martin Costello, 2024. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Microsoft.Extensions.Logging.Abstractions;

namespace MartinCostello.DotNetBumper;

public class LogReaderTests
{
    [Fact]
    public async Task GetTestLogsFromTrxAsync_Reads_Outcomes_And_Summary()
    {
        // Arrange
        using var directory = new TemporaryDirectory();

        const string Trx =
            """
            <?xml version="1.0" encoding="UTF-8"?>
            <TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
              <Results>
                <UnitTestResult testName="Project.Tests.PassingTest" outcome="Passed" />
                <UnitTestResult testName="Project.Tests.SkippedTest" outcome="NotExecuted" />
                <UnitTestResult testName="Project.Tests.FailingTest" outcome="Failed">
                  <Output>
                    <ErrorInfo>
                      <Message>Expected true but was false.</Message>
                    </ErrorInfo>
                  </Output>
                </UnitTestResult>
              </Results>
              <TestDefinitions>
                <UnitTest name="Project.Tests.PassingTest" storage="/code/tests/project.tests.dll" />
              </TestDefinitions>
              <ResultSummary outcome="Failed">
                <Counters total="3" executed="2" passed="1" failed="1" notExecuted="1" />
              </ResultSummary>
            </TestRun>
            """;

        await File.WriteAllTextAsync(Path.Combine(directory.Path, "results.trx"), Trx, TestContext.Current.CancellationToken);

        // Act
        var actual = await LogReader.GetTestLogsFromTrxAsync(
            directory.Path,
            NullLogger.Instance,
            TestContext.Current.CancellationToken);

        // Assert
        actual.ShouldNotBeNull();

        const string Container = "project.tests";

        actual.Outcomes.ShouldContainKey(Container);
        actual.Outcomes[Container].Count.ShouldBe(3);

        var failure = actual.Outcomes[Container].Where((p) => p.Outcome is "Failed").ShouldHaveSingleItem();
        failure.Id.ShouldBe("Project.Tests.FailingTest");
        failure.ErrorMessage.ShouldBe("Expected true but was false.");

        actual.Summary.ShouldContainKey(Container);
        var summary = actual.Summary[Container];
        summary["Passed"].ShouldBe(1);
        summary["Failed"].ShouldBe(1);
        summary["Skipped"].ShouldBe(1);
    }

    [Fact]
    public async Task GetTestLogsFromTrxAsync_Returns_Empty_When_No_Files()
    {
        // Arrange
        using var directory = new TemporaryDirectory();

        // Act
        var actual = await LogReader.GetTestLogsFromTrxAsync(
            directory.Path,
            NullLogger.Instance,
            TestContext.Current.CancellationToken);

        // Assert
        actual.ShouldNotBeNull();
        actual.Outcomes.ShouldBeEmpty();
        actual.Summary.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetTestLogsFromTrxAsync_Ignores_Malformed_Trx()
    {
        // Arrange
        using var directory = new TemporaryDirectory();

        await File.WriteAllTextAsync(Path.Combine(directory.Path, "broken.trx"), "<not-valid", TestContext.Current.CancellationToken);

        // Act
        var actual = await LogReader.GetTestLogsFromTrxAsync(
            directory.Path,
            NullLogger.Instance,
            TestContext.Current.CancellationToken);

        // Assert
        actual.ShouldNotBeNull();
        actual.Outcomes.ShouldBeEmpty();
        actual.Summary.ShouldBeEmpty();
    }
}
