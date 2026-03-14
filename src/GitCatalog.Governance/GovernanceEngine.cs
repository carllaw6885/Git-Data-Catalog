
using GitCatalog.Core;

namespace GitCatalog.Governance;

public static class GovernanceEngine
{
    public static IEnumerable<string> Lint(IEnumerable<TableDefinition> tables)
    {
        foreach (var table in tables)
        {
            if (string.IsNullOrWhiteSpace(table.Owner.Team))
            {
                yield return $"WARN: Missing owner team on table '{table.Id}'.";
            }

            if (!string.IsNullOrWhiteSpace(table.Description) && table.Description.Length < 15)
            {
                yield return $"WARN: Description is too short on table '{table.Id}'.";
            }

            foreach (var column in table.Columns)
            {
                if (string.IsNullOrWhiteSpace(column.Description))
                {
                    yield return $"WARN: Missing description for column '{table.Id}.{column.Name}'.";
                }
            }
        }
    }
}
