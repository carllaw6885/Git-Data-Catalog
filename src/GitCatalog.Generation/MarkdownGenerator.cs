using System.Text;
using GitCatalog.Core;

namespace GitCatalog.Generation;

public static class MarkdownGenerator
{
    public static IReadOnlyList<GeneratedDocument> GenerateCatalogDocs(
        IEnumerable<TableDefinition> tables,
        IEnumerable<string> governanceWarnings)
    {
        var tableList = tables.OrderBy(t => t.Id, StringComparer.OrdinalIgnoreCase).ToList();
        var warnings = governanceWarnings.ToList();
        var documents = new List<GeneratedDocument>
        {
            new("index.md", BuildIndex(tableList, warnings))
        };

        foreach (var table in tableList)
        {
            documents.Add(new GeneratedDocument($"tables/{table.Id}.md", BuildTablePage(table)));
        }

        return documents;
    }

    private static string BuildIndex(IEnumerable<TableDefinition> tables, IReadOnlyList<string> governanceWarnings)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# GitCatalog Generated Documentation");
        sb.AppendLine();
        sb.AppendLine("## Tables");
        sb.AppendLine();

        foreach (var table in tables)
        {
            sb.AppendLine($"- [{table.Id}](tables/{table.Id}.md)");
        }

        sb.AppendLine();
        sb.AppendLine("## Governance Findings");
        sb.AppendLine();

        if (governanceWarnings.Count == 0)
        {
            sb.AppendLine("No governance warnings.");
            return sb.ToString();
        }

        foreach (var warning in governanceWarnings)
        {
            sb.AppendLine($"- {warning}");
        }

        return sb.ToString();
    }

    private static string BuildTablePage(TableDefinition table)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# {table.Id}");
        sb.AppendLine();
        sb.AppendLine("## Metadata");
        sb.AppendLine();
        sb.AppendLine($"- Database: {table.Database}");
        sb.AppendLine($"- Schema: {table.Schema}");
        sb.AppendLine($"- Owner Team: {table.Owner.Team}");
        sb.AppendLine();
        sb.AppendLine("## Description");
        sb.AppendLine();
        sb.AppendLine(table.Description);
        sb.AppendLine();
        sb.AppendLine("## Columns");
        sb.AppendLine();
        sb.AppendLine("| Name | Type | PK | FK | Description |");
        sb.AppendLine("| --- | --- | --- | --- | --- |");

        foreach (var column in table.Columns)
        {
            sb.AppendLine($"| {column.Name} | {column.Type} | {(column.Pk ? "Yes" : "No")} | {column.Fk ?? ""} | {column.Description} |");
        }

        return sb.ToString();
    }
}

public sealed record GeneratedDocument(string RelativePath, string Content);