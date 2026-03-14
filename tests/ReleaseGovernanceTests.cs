using GitCatalog.Governance;

namespace GitCatalog.Tests;

public class ReleaseGovernanceTests
{
    [Fact]
    public void Evaluate_Fails_On_Warnings_When_Configured()
    {
        var tableFindings = new List<GovernanceFinding>
        {
            new(GovernanceSeverity.Warn, "warn1", "warning")
        };

        var graphFindings = new List<GovernanceFinding>();
        var result = ReleaseGovernance.Evaluate([], tableFindings, graphFindings, failOnWarn: true);

        Assert.False(result.IsReady);
        Assert.Equal(1, result.WarningCount);
        Assert.Equal(0, result.ErrorCount);
    }

    [Fact]
    public void Evaluate_Allows_Warnings_When_Failing_On_Errors_Only()
    {
        var tableFindings = new List<GovernanceFinding>
        {
            new(GovernanceSeverity.Warn, "warn1", "warning")
        };

        var graphFindings = new List<GovernanceFinding>();
        var result = ReleaseGovernance.Evaluate([], tableFindings, graphFindings, failOnWarn: false);

        Assert.True(result.IsReady);
        Assert.Equal(1, result.WarningCount);
        Assert.Equal(0, result.ErrorCount);
    }
}
