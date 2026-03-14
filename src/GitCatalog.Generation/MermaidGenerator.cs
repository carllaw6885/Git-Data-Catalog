
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

    public static string GenerateGraphView(CatalogGraph graph, CatalogViewpoint viewpoint)
    {
        var filtered = CatalogViewpointService.Filter(graph, viewpoint);
        var sb = new StringBuilder();
        var layout = string.IsNullOrWhiteSpace(viewpoint.Layout) ? "LR" : viewpoint.Layout;
        sb.AppendLine($"flowchart {layout}");

        var idMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var index = 0;

        foreach (var entity in filtered.Entities)
        {
            var nodeId = $"N{index++}";
            idMap[entity.Id] = nodeId;

            var label = string.IsNullOrWhiteSpace(entity.Title)
                ? (string.IsNullOrWhiteSpace(entity.Name) ? entity.Id : entity.Name)
                : entity.Title;

            sb.AppendLine($"  {nodeId}[\"{Escape(label)}\"]");
        }

        foreach (var rel in filtered.Relationships)
        {
            if (!idMap.TryGetValue(rel.From, out var fromId) || !idMap.TryGetValue(rel.To, out var toId))
            {
                continue;
            }

            var label = ToRelationshipLabel(rel.Type);
            sb.AppendLine($"  {fromId} -->|{label}| {toId}");
        }

        return sb.ToString();
    }

    private static string ToEntityName(string id) => id.Replace('.', '_');

    private static string ToRelationshipLabel(CatalogRelationshipType type)
    {
        var raw = type.ToString();
        var chars = new List<char>(raw.Length + 8);
        foreach (var c in raw)
        {
            if (char.IsUpper(c) && chars.Count > 0)
            {
                chars.Add('_');
            }

            chars.Add(char.ToLowerInvariant(c));
        }

        return new string(chars.ToArray());
    }

    private static string Escape(string value)
        => value.Replace("\"", "\\\"", StringComparison.Ordinal);

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
