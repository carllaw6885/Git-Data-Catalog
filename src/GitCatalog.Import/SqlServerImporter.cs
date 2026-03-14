using GitCatalog.Core;
using Microsoft.Data.SqlClient;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace GitCatalog.Import;

public sealed class SqlServerImporter
{
    public async Task<ImportResult> ImportAsync(
        string connectionString,
        string repoRoot,
        ImportOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new ImportOptions();

        var rows = await ReadSchemaAsync(connectionString, cancellationToken);
        var importedTables = BuildTables(rows.Tables, rows.Columns, rows.ForeignKeys);
        var plan = BuildImportPlan(importedTables, repoRoot);

        var files = new List<string>();
        if (!options.DryRun)
        {
            foreach (var write in plan.Writes)
            {
                var directory = Path.GetDirectoryName(write.FilePath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(write.FilePath, write.Content);
                files.Add(write.FilePath);
            }
        }

        return new ImportResult(plan.Tables, files, plan.Warnings, plan.Changes, options.DryRun);
    }

    public ImportPlan BuildImportPlan(IReadOnlyCollection<TableDefinition> importedTables, string repoRoot)
    {
        var outputPath = Path.Combine(repoRoot, "catalog", "tables");

        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        var serializer = new SerializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

        var mergedTables = new List<TableDefinition>();
        var writes = new List<PlannedCatalogWrite>();
        var changes = new List<ImportChange>();
        var warnings = new List<string>();
        var importedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var imported in importedTables.OrderBy(t => t.Id, StringComparer.OrdinalIgnoreCase))
        {
            importedIds.Add(imported.Id);

            var path = Path.Combine(outputPath, $"{imported.Id}.yaml");
            var merged = imported;
            var changeKind = ImportChangeKind.Create;
            var driftDetails = new List<string>();

            if (File.Exists(path))
            {
                try
                {
                    var existingText = File.ReadAllText(path);
                    var existing = deserializer.Deserialize<TableDefinition>(existingText);
                    if (existing is not null)
                    {
                        driftDetails.AddRange(DetectTableDrift(existing, imported));
                        merged = MergeWithExisting(imported, existing);
                        driftDetails.AddRange(DetectMergeDecisions(existing, imported, merged));

                        changeKind = AreEquivalent(existing, merged)
                            ? ImportChangeKind.Unchanged
                            : ImportChangeKind.Update;
                    }
                    else
                    {
                        changeKind = ImportChangeKind.Update;
                        driftDetails.Add("Existing table metadata could not be parsed; update planned from source schema.");
                    }
                }
                catch (Exception ex)
                {
                    warnings.Add($"Unable to merge existing metadata for '{imported.Id}' from {path}: {ex.Message}");
                    changeKind = ImportChangeKind.Update;
                    driftDetails.Add("Merge fallback activated due to metadata parse error.");
                }
            }
            else
            {
                driftDetails.Add("Table does not exist in catalog; file will be created.");
            }

            var yaml = serializer.Serialize(merged);
            mergedTables.Add(merged);
            changes.Add(new ImportChange(imported.Id, path, changeKind, BuildChangeSummary(changeKind, imported.Id), driftDetails));

            if (changeKind is ImportChangeKind.Create or ImportChangeKind.Update)
            {
                writes.Add(new PlannedCatalogWrite(path, yaml, merged));
            }
        }

        if (!Directory.Exists(outputPath))
        {
            return new ImportPlan(mergedTables, writes, changes, warnings);
        }

        foreach (var existingFile in Directory.GetFiles(outputPath, "*.yaml", SearchOption.AllDirectories))
        {
            var tableId = Path.GetFileNameWithoutExtension(existingFile);
            if (importedIds.Contains(tableId))
            {
                continue;
            }

            changes.Add(new ImportChange(
                tableId,
                existingFile,
                ImportChangeKind.Remove,
                BuildChangeSummary(ImportChangeKind.Remove, tableId),
                ["Table exists in catalog but is absent from source schema."]));
        }

        return new ImportPlan(mergedTables, writes, changes, warnings);
    }

    public TableDefinition MergeWithExisting(TableDefinition imported, TableDefinition existing)
    {
        var existingColumns = existing.Columns
            .ToDictionary(c => c.Name, StringComparer.OrdinalIgnoreCase);

        var mergedColumns = imported.Columns
            .Select(c => MergeColumn(c, existingColumns.TryGetValue(c.Name, out var existingColumn) ? existingColumn : null))
            .ToList();

        return new TableDefinition
        {
            Id = imported.Id,
            Database = imported.Database,
            Schema = imported.Schema,
            Description = string.IsNullOrWhiteSpace(existing.Description) ? imported.Description : existing.Description,
            Owner = new OwnerDefinition
            {
                Team = string.IsNullOrWhiteSpace(existing.Owner?.Team) ? imported.Owner.Team : existing.Owner.Team
            },
            Columns = mergedColumns
        };
    }

    public IReadOnlyList<TableDefinition> BuildTables(
        IReadOnlyCollection<SqlTableRow> tables,
        IReadOnlyCollection<SqlColumnRow> columns,
        IReadOnlyCollection<SqlForeignKeyRow> foreignKeys)
    {
        var fkLookup = foreignKeys
            .GroupBy(fk => (fk.SchemaName, fk.TableName, fk.ColumnName), StringTupleComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringTupleComparer.OrdinalIgnoreCase);

        var result = new List<TableDefinition>();
        foreach (var table in tables.OrderBy(t => t.SchemaName).ThenBy(t => t.TableName))
        {
            var tableColumns = columns
                .Where(c => c.SchemaName.Equals(table.SchemaName, StringComparison.OrdinalIgnoreCase)
                    && c.TableName.Equals(table.TableName, StringComparison.OrdinalIgnoreCase))
                .OrderBy(c => c.OrdinalPosition)
                .Select(c => new ColumnDefinition
                {
                    Name = c.ColumnName,
                    Type = c.DataType,
                    Pk = c.IsPrimaryKey,
                    Fk = fkLookup.TryGetValue((c.SchemaName, c.TableName, c.ColumnName), out var fk)
                        ? $"{BuildTableId(table.DatabaseName, fk.ReferencedSchema, fk.ReferencedTable)}.{fk.ReferencedColumn}"
                        : null,
                    Description = ""
                })
                .ToList();

            result.Add(new TableDefinition
            {
                Id = BuildTableId(table.DatabaseName, table.SchemaName, table.TableName),
                Database = table.DatabaseName,
                Schema = table.SchemaName,
                Description = $"Imported from SQL Server table {table.SchemaName}.{table.TableName}",
                Owner = new OwnerDefinition { Team = "" },
                Columns = tableColumns
            });
        }

        return result;
    }

    private static ColumnDefinition MergeColumn(ColumnDefinition imported, ColumnDefinition? existing)
    {
        if (existing is null)
        {
            return imported;
        }

        return new ColumnDefinition
        {
            Name = imported.Name,
            Type = imported.Type,
            Pk = imported.Pk,
            Fk = string.IsNullOrWhiteSpace(imported.Fk) ? existing.Fk : imported.Fk,
            Description = string.IsNullOrWhiteSpace(existing.Description) ? imported.Description : existing.Description
        };
    }

    private static string BuildTableId(string databaseName, string schemaName, string tableName)
        => schemaName.Equals("dbo", StringComparison.OrdinalIgnoreCase)
            ? $"{databaseName}.{tableName}"
            : $"{databaseName}.{schemaName}.{tableName}";

    private static string BuildChangeSummary(ImportChangeKind kind, string tableId)
        => kind switch
        {
            ImportChangeKind.Create => $"Create {tableId}",
            ImportChangeKind.Update => $"Update {tableId}",
            ImportChangeKind.Unchanged => $"No changes for {tableId}",
            ImportChangeKind.Remove => $"Existing catalog file not present in source schema: {tableId}",
            _ => tableId
        };

    private static IReadOnlyList<string> DetectTableDrift(TableDefinition existing, TableDefinition imported)
    {
        var drift = new List<string>();

        if (!string.Equals(existing.Database, imported.Database, StringComparison.OrdinalIgnoreCase))
        {
            drift.Add($"Database changed: {existing.Database} -> {imported.Database}");
        }

        if (!string.Equals(existing.Schema, imported.Schema, StringComparison.OrdinalIgnoreCase))
        {
            drift.Add($"Schema changed: {existing.Schema} -> {imported.Schema}");
        }

        var existingColumns = existing.Columns.ToDictionary(c => c.Name, StringComparer.OrdinalIgnoreCase);
        var importedColumns = imported.Columns.ToDictionary(c => c.Name, StringComparer.OrdinalIgnoreCase);

        foreach (var name in importedColumns.Keys.Except(existingColumns.Keys, StringComparer.OrdinalIgnoreCase).OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
        {
            drift.Add($"Column added: {name}");
        }

        foreach (var name in existingColumns.Keys.Except(importedColumns.Keys, StringComparer.OrdinalIgnoreCase).OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
        {
            drift.Add($"Column removed: {name}");
        }

        foreach (var name in importedColumns.Keys.Intersect(existingColumns.Keys, StringComparer.OrdinalIgnoreCase).OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
        {
            var current = existingColumns[name];
            var source = importedColumns[name];

            if (!string.Equals(current.Type, source.Type, StringComparison.OrdinalIgnoreCase))
            {
                drift.Add($"Column type changed: {name} {current.Type} -> {source.Type}");
            }

            if (current.Pk != source.Pk)
            {
                drift.Add($"Column PK changed: {name} {current.Pk} -> {source.Pk}");
            }

            if (!string.Equals(current.Fk ?? string.Empty, source.Fk ?? string.Empty, StringComparison.OrdinalIgnoreCase))
            {
                drift.Add($"Column FK changed: {name} {(current.Fk ?? "<none>")} -> {(source.Fk ?? "<none>")}");
            }
        }

        return drift;
    }

    private static bool AreEquivalent(TableDefinition left, TableDefinition right)
    {
        if (!string.Equals(left.Id, right.Id, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(left.Database, right.Database, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(left.Schema, right.Schema, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(left.Description, right.Description, StringComparison.Ordinal)
            || !string.Equals(left.Owner.Team, right.Owner.Team, StringComparison.Ordinal)
            || left.Columns.Count != right.Columns.Count)
        {
            return false;
        }

        for (var i = 0; i < left.Columns.Count; i++)
        {
            var a = left.Columns[i];
            var b = right.Columns[i];
            if (!string.Equals(a.Name, b.Name, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(a.Type, b.Type, StringComparison.OrdinalIgnoreCase)
                || a.Pk != b.Pk
                || !string.Equals(a.Fk ?? string.Empty, b.Fk ?? string.Empty, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(a.Description, b.Description, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static async Task<SchemaRows> ReadSchemaAsync(string connectionString, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        var databaseName = connection.Database;

        var tables = new List<SqlTableRow>();
        var columns = new List<SqlColumnRow>();
        var foreignKeys = new List<SqlForeignKeyRow>();

        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = """
            SELECT TABLE_SCHEMA, TABLE_NAME
            FROM INFORMATION_SCHEMA.TABLES
            WHERE TABLE_TYPE = 'BASE TABLE'
            ORDER BY TABLE_SCHEMA, TABLE_NAME;
            """;

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                tables.Add(new SqlTableRow(
                    databaseName,
                    reader.GetString(0),
                    reader.GetString(1)));
            }
        }

        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = """
            SELECT
                c.TABLE_SCHEMA,
                c.TABLE_NAME,
                c.COLUMN_NAME,
                c.DATA_TYPE,
                c.ORDINAL_POSITION,
                CASE WHEN k.COLUMN_NAME IS NULL THEN 0 ELSE 1 END AS IS_PK
            FROM INFORMATION_SCHEMA.COLUMNS c
            LEFT JOIN (
                SELECT ku.TABLE_SCHEMA, ku.TABLE_NAME, ku.COLUMN_NAME
                FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
                JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE ku
                    ON tc.CONSTRAINT_NAME = ku.CONSTRAINT_NAME
                    AND tc.TABLE_SCHEMA = ku.TABLE_SCHEMA
                WHERE tc.CONSTRAINT_TYPE = 'PRIMARY KEY'
            ) k
              ON c.TABLE_SCHEMA = k.TABLE_SCHEMA
             AND c.TABLE_NAME = k.TABLE_NAME
             AND c.COLUMN_NAME = k.COLUMN_NAME
            ORDER BY c.TABLE_SCHEMA, c.TABLE_NAME, c.ORDINAL_POSITION;
            """;

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                columns.Add(new SqlColumnRow(
                    reader.GetString(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetString(3),
                    reader.GetInt32(4),
                    reader.GetInt32(5) == 1));
            }
        }

        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = """
            SELECT
                sch1.name AS FK_SCHEMA,
                tab1.name AS FK_TABLE,
                col1.name AS FK_COLUMN,
                sch2.name AS REF_SCHEMA,
                tab2.name AS REF_TABLE,
                col2.name AS REF_COLUMN
            FROM sys.foreign_key_columns fkc
            JOIN sys.tables tab1 ON fkc.parent_object_id = tab1.object_id
            JOIN sys.schemas sch1 ON tab1.schema_id = sch1.schema_id
            JOIN sys.columns col1 ON fkc.parent_object_id = col1.object_id AND fkc.parent_column_id = col1.column_id
            JOIN sys.tables tab2 ON fkc.referenced_object_id = tab2.object_id
            JOIN sys.schemas sch2 ON tab2.schema_id = sch2.schema_id
            JOIN sys.columns col2 ON fkc.referenced_object_id = col2.object_id AND fkc.referenced_column_id = col2.column_id
            ORDER BY sch1.name, tab1.name;
            """;

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                foreignKeys.Add(new SqlForeignKeyRow(
                    reader.GetString(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetString(3),
                    reader.GetString(4),
                    reader.GetString(5)));
            }
        }

        return new SchemaRows(tables, columns, foreignKeys);
    }

    private sealed class StringTupleComparer : IEqualityComparer<(string, string, string)>
    {
        public static readonly StringTupleComparer OrdinalIgnoreCase = new();

        public bool Equals((string, string, string) x, (string, string, string) y)
            => string.Equals(x.Item1, y.Item1, StringComparison.OrdinalIgnoreCase)
               && string.Equals(x.Item2, y.Item2, StringComparison.OrdinalIgnoreCase)
               && string.Equals(x.Item3, y.Item3, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode((string, string, string) obj)
            => HashCode.Combine(
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Item1),
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Item2),
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Item3));
    }

    private sealed record SchemaRows(
        IReadOnlyCollection<SqlTableRow> Tables,
        IReadOnlyCollection<SqlColumnRow> Columns,
        IReadOnlyCollection<SqlForeignKeyRow> ForeignKeys);

    private static IReadOnlyList<string> DetectMergeDecisions(TableDefinition existing, TableDefinition imported, TableDefinition merged)
    {
        var decisions = new List<string>();

        if (string.Equals(merged.Description, existing.Description, StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(existing.Description))
        {
            decisions.Add("Merge strategy: kept curated table description from existing metadata.");
        }
        else if (string.Equals(merged.Description, imported.Description, StringComparison.Ordinal))
        {
            decisions.Add("Merge strategy: used imported table description due to empty curated value.");
        }

        var existingOwner = existing.Owner?.Team ?? string.Empty;
        var importedOwner = imported.Owner?.Team ?? string.Empty;
        var mergedOwner = merged.Owner?.Team ?? string.Empty;

        if (!string.IsNullOrWhiteSpace(existingOwner)
            && string.Equals(mergedOwner, existingOwner, StringComparison.Ordinal))
        {
            decisions.Add("Merge strategy: kept curated owner team from existing metadata.");
        }
        else if (string.Equals(mergedOwner, importedOwner, StringComparison.Ordinal))
        {
            decisions.Add("Merge strategy: used imported owner team due to empty curated value.");
        }

        var existingColumns = existing.Columns.ToDictionary(c => c.Name, StringComparer.OrdinalIgnoreCase);
        var importedColumns = imported.Columns.ToDictionary(c => c.Name, StringComparer.OrdinalIgnoreCase);

        foreach (var column in merged.Columns.OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase))
        {
            if (!existingColumns.TryGetValue(column.Name, out var existingColumn)
                || !importedColumns.TryGetValue(column.Name, out var importedColumn))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(existingColumn.Description)
                && string.Equals(column.Description, existingColumn.Description, StringComparison.Ordinal))
            {
                decisions.Add($"Merge strategy: kept curated description for column '{column.Name}'.");
            }
            else if (string.Equals(column.Description, importedColumn.Description, StringComparison.Ordinal)
                     && string.IsNullOrWhiteSpace(existingColumn.Description))
            {
                decisions.Add($"Merge strategy: used imported description for column '{column.Name}' (curated value empty).");
            }

            var existingFk = existingColumn.Fk ?? string.Empty;
            var importedFk = importedColumn.Fk ?? string.Empty;
            var mergedFk = column.Fk ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(existingFk)
                && string.Equals(mergedFk, existingFk, StringComparison.OrdinalIgnoreCase)
                && string.IsNullOrWhiteSpace(importedFk))
            {
                decisions.Add($"Merge strategy: retained curated FK mapping for column '{column.Name}'.");
            }
            else if (string.Equals(mergedFk, importedFk, StringComparison.OrdinalIgnoreCase)
                     && !string.IsNullOrWhiteSpace(importedFk))
            {
                decisions.Add($"Merge strategy: applied imported FK mapping for column '{column.Name}'.");
            }
        }

        return decisions;
    }

}

public sealed record ImportOptions(bool DryRun = false);
public sealed record ImportPlan(
    IReadOnlyList<TableDefinition> Tables,
    IReadOnlyList<PlannedCatalogWrite> Writes,
    IReadOnlyList<ImportChange> Changes,
    IReadOnlyList<string> Warnings);
public sealed record PlannedCatalogWrite(string FilePath, string Content, TableDefinition Table);
public sealed record ImportResult(
    IReadOnlyList<TableDefinition> Tables,
    IReadOnlyList<string> FilesWritten,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<ImportChange> Changes,
    bool IsDryRun);
public sealed record ImportChange(
    string TableId,
    string? FilePath,
    ImportChangeKind Kind,
    string Summary,
    IReadOnlyList<string> DriftDetails);
public enum ImportChangeKind
{
    Create,
    Update,
    Unchanged,
    Remove
}

public sealed record SqlTableRow(string DatabaseName, string SchemaName, string TableName);
public sealed record SqlColumnRow(string SchemaName, string TableName, string ColumnName, string DataType, int OrdinalPosition, bool IsPrimaryKey);
public sealed record SqlForeignKeyRow(string SchemaName, string TableName, string ColumnName, string ReferencedSchema, string ReferencedTable, string ReferencedColumn);
