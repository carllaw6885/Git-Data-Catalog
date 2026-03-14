
using GitCatalog.Core;

namespace GitCatalog.Validation;

public static class CatalogValidator
{
    public static IEnumerable<string> Validate(IEnumerable<TableDefinition> tables)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var table in tables)
        {
            if (string.IsNullOrWhiteSpace(table.Id))
            {
                yield return "Table ID is required.";
            }
            else if (!seen.Add(table.Id))
            {
                yield return $"Duplicate table ID found: {table.Id}";
            }

            if (string.IsNullOrWhiteSpace(table.Database))
            {
                yield return $"Database is required for table '{table.Id}'.";
            }

            if (string.IsNullOrWhiteSpace(table.Schema))
            {
                yield return $"Schema is required for table '{table.Id}'.";
            }

            if (string.IsNullOrWhiteSpace(table.Description))
            {
                yield return $"Description is required for table '{table.Id}'.";
            }

            if (table.Columns.Count == 0)
            {
                yield return $"At least one column is required for table '{table.Id}'.";
            }

            foreach (var column in table.Columns)
            {
                if (string.IsNullOrWhiteSpace(column.Name))
                {
                    yield return $"Column name is required for table '{table.Id}'.";
                }

                if (string.IsNullOrWhiteSpace(column.Type))
                {
                    yield return $"Column type is required for '{table.Id}.{column.Name}'.";
                }
            }
        }
    }
}
