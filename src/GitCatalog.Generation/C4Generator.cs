using GitCatalog.Core;
using System.Text;

namespace GitCatalog.Generation;

public static class C4Generator
{
    public static GeneratedAsset GenerateContext(CatalogGraph graph)
        => Generate(graph, C4Level.Context, "c4-context", "c4/context.mmd", "LR", "C");

    public static GeneratedAsset GenerateContainer(CatalogGraph graph)
        => Generate(graph, C4Level.Container, "c4-container", "c4/container.mmd", "LR", "K");

    public static GeneratedAsset GenerateComponent(CatalogGraph graph)
        => Generate(graph, C4Level.Component, "c4-component", "c4/component.mmd", "TB", "P");

    private static GeneratedAsset Generate(
        CatalogGraph graph,
        C4Level level,
        string viewpointId,
        string relativePath,
        string fallbackLayout,
        string nodePrefix)
    {
        var model = C4ModelBuilder.Build(graph, level);
        var layout = ResolveLayout(graph, viewpointId, fallbackLayout);

        var sb = new StringBuilder();
        sb.AppendLine($"flowchart {layout}");

        var nodeIds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var index = 0;

        foreach (var node in model.Nodes.OrderBy(n => n.Id, StringComparer.OrdinalIgnoreCase))
        {
            var nodeId = $"{nodePrefix}{index++}";
            nodeIds[node.Id] = nodeId;

            var label = string.IsNullOrWhiteSpace(node.Label) ? node.Id : node.Label;
            sb.AppendLine($"  {nodeId}[\"{Escape(label)}\"]");
        }

        foreach (var edge in model.Edges.OrderBy(e => e.Id, StringComparer.OrdinalIgnoreCase))
        {
            if (!nodeIds.TryGetValue(edge.From, out var from) || !nodeIds.TryGetValue(edge.To, out var to))
            {
                continue;
            }

            var label = ToRelationshipLabel(edge.Type);
            sb.AppendLine($"  {from} -->|{label}| {to}");
        }

        return new GeneratedAsset(relativePath, sb.ToString());
    }

    private static string ResolveLayout(CatalogGraph graph, string viewpointId, string fallback)
    {
        var viewpoint = graph.Viewpoints.FirstOrDefault(v => v.Id.Equals(viewpointId, StringComparison.OrdinalIgnoreCase));
        if (viewpoint is null || string.IsNullOrWhiteSpace(viewpoint.Layout))
        {
            return fallback;
        }

        return viewpoint.Layout;
    }

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
}
