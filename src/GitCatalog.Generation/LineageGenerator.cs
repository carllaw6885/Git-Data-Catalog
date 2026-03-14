using GitCatalog.Core;

namespace GitCatalog.Generation;

public static class LineageGenerator
{
    public static IReadOnlyList<GeneratedAsset> Generate(CatalogGraph graph)
    {
        var lineageViewpoints = graph.Viewpoints
            .Where(IsLineageViewpoint)
            .OrderBy(v => v.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var assets = new List<GeneratedAsset>();
        foreach (var viewpoint in lineageViewpoints)
        {
            var content = MermaidGenerator.GenerateGraphView(graph, viewpoint);
            assets.Add(new GeneratedAsset($"lineage/{viewpoint.Id}.mmd", content));
        }

        return assets;
    }

    private static bool IsLineageViewpoint(CatalogViewpoint viewpoint)
        => viewpoint.Id.Contains("lineage", StringComparison.OrdinalIgnoreCase)
            || viewpoint.Name.Contains("lineage", StringComparison.OrdinalIgnoreCase);
}

public sealed record GeneratedAsset(string RelativePath, string Content);
