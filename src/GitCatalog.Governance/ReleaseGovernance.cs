using GitCatalog.Core;

namespace GitCatalog.Governance;

public sealed record ReleaseGateResult(
    bool IsReady,
    int ValidationErrorCount,
    int WarningCount,
    int ErrorCount,
    IReadOnlyList<string> Messages);

public static class ReleaseGovernance
{
    public static ReleaseGateResult Evaluate(
        IReadOnlyList<string> validationErrors,
        IReadOnlyList<GovernanceFinding> tableFindings,
        IReadOnlyList<GovernanceFinding> graphFindings,
        bool failOnWarn)
    {
        var allFindings = tableFindings.Concat(graphFindings).ToList();

        var warningCount = allFindings.Count(f => f.Severity == GovernanceSeverity.Warn);
        var errorCount = allFindings.Count(f => f.Severity == GovernanceSeverity.Error);
        var hasBlockingFindings = failOnWarn
            ? warningCount > 0 || errorCount > 0
            : errorCount > 0;

        var ready = validationErrors.Count == 0 && !hasBlockingFindings;

        var messages = new List<string>
        {
            $"Validation errors: {validationErrors.Count}",
            $"Governance warnings: {warningCount}",
            $"Governance errors: {errorCount}",
            $"Fail on warnings: {failOnWarn}"
        };

        if (!ready)
        {
            messages.Add("Release governance check failed.");
        }
        else
        {
            messages.Add("Release governance check passed.");
        }

        return new ReleaseGateResult(ready, validationErrors.Count, warningCount, errorCount, messages);
    }
}
