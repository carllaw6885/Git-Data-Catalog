
using System.Text;
using GitCatalog.Core;

namespace GitCatalog.Generation;

public static class MermaidGenerator
{
    public static string GenerateEr(IEnumerable<TableDefinition> tables)
    {
        var tableList = tables.ToList();
        var sb = new StringBuilder();
        sb.AppendLine("erDiagram");

        foreach (var t in tableList)
        {
            var entity = ToEntityName(t.Id);
            sb.AppendLine($"  {entity} {{");

            foreach (var c in t.Columns)
            {
                var pkTag = c.Pk ? " PK" : string.Empty;
                sb.AppendLine($"    {c.Type} {c.Name}{pkTag}");
            }

            sb.AppendLine("  }");
        }

        foreach (var t in tableList)
        {
            foreach (var c in t.Columns.Where(c => !string.IsNullOrWhiteSpace(c.Fk)))
            {
                var targetTable = ParseForeignKeyTargetTable(c.Fk!);
                if (string.IsNullOrWhiteSpace(targetTable))
                {
                    continue;
                }

                sb.AppendLine($"  {ToEntityName(t.Id)} }}o--|| {ToEntityName(targetTable)} : {c.Name}");
            }
        }

        return sb.ToString();
    }

    private static string ToEntityName(string id) => id.Replace('.', '_');

    private static string? ParseForeignKeyTargetTable(string fk)
    {
        var parts = fk.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 3)
        {
            return null;
        }

        return string.Join('.', parts.Take(parts.Length - 1));
    }
}
