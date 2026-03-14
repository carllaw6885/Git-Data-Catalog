using GitCatalog.Core;
using Microsoft.Data.SqlClient;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace GitCatalog.Import;

public sealed class SqlServerImporter
{
    public async Task<ImportResult> ImportAsync(string connectionString, string repoRoot, CancellationToken cancellationToken = default)
    {
        var rows = await ReadSchemaAsync(connectionString, cancellationToken);
        var importedTables = BuildTables(rows.Tables, rows.Columns, rows.ForeignKeys);

        var outputPath = Path.Combine(repoRoot, "catalog", "tables");
        Directory.CreateDirectory(outputPath);

        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        var serializer = new SerializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

        var mergedTables = new List<TableDefinition>();
        var files = new List<string>();
        foreach (var imported in importedTables)
        {
            var path = Path.Combine(outputPath, $"{imported.Id}.yaml");
            var merged = imported;

            if (File.Exists(path))
            {
                var existingText = File.ReadAllText(path);
                var existing = deserializer.Deserialize<TableDefinition>(existingText);
                if (existing is not null)
                {
                    merged = MergeWithExisting(imported, existing);
                }
            }

            var yaml = serializer.Serialize(merged);
            File.WriteAllText(path, yaml);
            files.Add(path);
            mergedTables.Add(merged);
        }

        return new ImportResult(mergedTables, files);
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
                        ? $"{table.DatabaseName}.{fk.ReferencedSchema}.{fk.ReferencedTable}.{fk.ReferencedColumn}"
                        : null,
                    Description = ""
                })
                .ToList();

            result.Add(new TableDefinition
            {
                Id = $"{table.DatabaseName}.{table.TableName}",
                Database = table.DatabaseName,
                Schema = table.SchemaName,
                Description = $"Imported from SQL Server table {table.SchemaName}.{table.TableName}",
                Owner = new OwnerDefinition { Team = "" },
                Columns = tableColumns
            });
        }

        return result;
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
}

public sealed record ImportResult(IReadOnlyList<TableDefinition> Tables, IReadOnlyList<string> FilesWritten);

public sealed record SqlTableRow(string DatabaseName, string SchemaName, string TableName);
public sealed record SqlColumnRow(string SchemaName, string TableName, string ColumnName, string DataType, int OrdinalPosition, bool IsPrimaryKey);
public sealed record SqlForeignKeyRow(string SchemaName, string TableName, string ColumnName, string ReferencedSchema, string ReferencedTable, string ReferencedColumn);