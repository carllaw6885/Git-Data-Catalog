
using GitCatalog.Core;

namespace GitCatalog.Validation;

public static class CatalogValidator
{
    public static IEnumerable<string> Validate(IEnumerable<TableDefinition> tables)
    {
        var tableList = tables.ToList();
        var tableLookup = tableList
            .Where(t => !string.IsNullOrWhiteSpace(t.Id))
            .GroupBy(t => t.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var table in tableList)
        {
            if (string.IsNullOrWhiteSpace(table.Id))
            {
                yield return "Table ID is required.";
            }
            else if (!seen.Add(table.Id))
            {
                yield return $"Duplicate table ID found: {table.Id}";
            }
            else if (!TryParseTableId(table.Id, out var _, out var _, out var _))
            {
                yield return $"Table ID '{table.Id}' must be '<database>.<table>' or '<database>.<schema>.<table>'.";
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

            var columnSeen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var column in table.Columns)
            {
                if (string.IsNullOrWhiteSpace(column.Name))
                {
                    yield return $"Column name is required for table '{table.Id}'.";
                }
                else if (!columnSeen.Add(column.Name))
                {
                    yield return $"Duplicate column name '{column.Name}' in table '{table.Id}'.";
                }

                if (string.IsNullOrWhiteSpace(column.Type))
                {
                    yield return $"Column type is required for '{table.Id}.{column.Name}'.";
                }

                if (string.IsNullOrWhiteSpace(column.Fk))
                {
                    continue;
                }

                if (!TryParseForeignKey(column.Fk!, out var targetTableId, out var targetColumn))
                {
                    yield return $"Invalid foreign key format '{column.Fk}' in '{table.Id}.{column.Name}'.";
                    continue;
                }

                if (!tableLookup.TryGetValue(targetTableId, out var targetTable))
                {
                    yield return $"Foreign key target table '{targetTableId}' not found for '{table.Id}.{column.Name}'.";
                    continue;
                }

                var hasTargetColumn = targetTable.Columns.Any(c =>
                    c.Name.Equals(targetColumn, StringComparison.OrdinalIgnoreCase));
                if (!hasTargetColumn)
                {
                    yield return $"Foreign key target column '{targetTableId}.{targetColumn}' not found for '{table.Id}.{column.Name}'.";
                }
            }
        }
    }

    private static bool TryParseTableId(string id, out string database, out string schema, out string table)
    {
        database = string.Empty;
        schema = string.Empty;
        table = string.Empty;

        var parts = id.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 2)
        {
            database = parts[0];
            schema = "dbo";
            table = parts[1];
            return true;
        }

        if (parts.Length == 3)
        {
            database = parts[0];
            schema = parts[1];
            table = parts[2];
            return true;
        }

        return false;
    }

    private static bool TryParseForeignKey(string fk, out string tableId, out string column)
    {
        tableId = string.Empty;
        column = string.Empty;

        var parts = fk.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 3)
        {
            tableId = string.Join('.', parts.Take(2));
            column = parts[2];
            return true;
        }

        if (parts.Length == 4)
        {
            tableId = string.Join('.', parts.Take(3));
            column = parts[3];
            return true;
        }

        return false;
    }
}
