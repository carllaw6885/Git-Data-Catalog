namespace GitCatalog.Core;

public enum C4Level
{
    Context,
    Container,
    Component
}

public sealed class C4Node
{
    public string Id { get; init; } = "";
    public CatalogEntityType Type { get; init; }
    public string Label { get; init; } = "";
    public string Description { get; init; } = "";
    public string? Technology { get; init; }
    public string? Boundary { get; init; }
    public string? Container { get; init; }
}

public sealed class C4Edge
{
    public string Id { get; init; } = "";
    public CatalogRelationshipType Type { get; init; }
    public string From { get; init; } = "";
    public string To { get; init; } = "";
    public string Description { get; init; } = "";
    public string? Technology { get; init; }
}

public sealed record C4Model(
    C4Level Level,
    IReadOnlyList<C4Node> Nodes,
    IReadOnlyList<C4Edge> Edges,
    IReadOnlyList<string> Diagnostics);

public static class C4ModelBuilder
{
    public static C4Model Build(CatalogGraph graph, C4Level level)
    {
        var (allowedEntities, allowedRelationships) = level switch
        {
            C4Level.Context =>
            (
                new HashSet<CatalogEntityType>
                {
                    CatalogEntityType.Actor,
                    CatalogEntityType.System,
                    CatalogEntityType.ExternalVendor,
                    CatalogEntityType.Consumer
                },
                new HashSet<CatalogRelationshipType>
                {
                    CatalogRelationshipType.Uses,
                    CatalogRelationshipType.PublishesTo,
                    CatalogRelationshipType.SyncsTo,
                    CatalogRelationshipType.DependsOn
                }
            ),
            C4Level.Container =>
            (
                new HashSet<CatalogEntityType>
                {
                    CatalogEntityType.Container,
                    CatalogEntityType.Database,
                    CatalogEntityType.Pipeline,
                    CatalogEntityType.Dataset,
                    CatalogEntityType.System,
                    CatalogEntityType.Interface
                },
                new HashSet<CatalogRelationshipType>
                {
                    CatalogRelationshipType.ReadsFrom,
                    CatalogRelationshipType.WritesTo,
                    CatalogRelationshipType.DependsOn,
                    CatalogRelationshipType.PublishesTo
                }
            ),
            C4Level.Component =>
            (
                new HashSet<CatalogEntityType>
                {
                    CatalogEntityType.Component,
                    CatalogEntityType.Container
                },
                new HashSet<CatalogRelationshipType>
                {
                    CatalogRelationshipType.Uses,
                    CatalogRelationshipType.Implements,
                    CatalogRelationshipType.Contains,
                    CatalogRelationshipType.DependsOn
                }
            ),
            _ =>
            (
                new HashSet<CatalogEntityType>(),
                new HashSet<CatalogRelationshipType>()
            )
        };

        var nodes = graph.Entities
            .Where(e => allowedEntities.Contains(e.Type))
            .OrderBy(e => e.Id, StringComparer.OrdinalIgnoreCase)
            .Select(e => new C4Node
            {
                Id = e.Id,
                Type = e.Type,
                Label = string.IsNullOrWhiteSpace(e.Title) ? e.Name : e.Title!,
                Description = e.Description,
                Technology = e.Technology,
                Boundary = e.Boundary,
                Container = e.Container
            })
            .ToList();

        var nodeIds = nodes.Select(n => n.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var edges = graph.Relationships
            .Where(r => allowedRelationships.Contains(r.Type))
            .Where(r => nodeIds.Contains(r.From) && nodeIds.Contains(r.To))
            .OrderBy(r => r.Id, StringComparer.OrdinalIgnoreCase)
            .Select(r => new C4Edge
            {
                Id = r.Id,
                Type = r.Type,
                From = r.From,
                To = r.To,
                Description = r.Description,
                Technology = r.Technology
            })
            .ToList();

        return new C4Model(level, nodes, edges, []);
    }
}
