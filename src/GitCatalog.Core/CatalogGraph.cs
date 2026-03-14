namespace GitCatalog.Core;

public sealed record CatalogGraph(
    IReadOnlyList<CatalogEntity> Entities,
    IReadOnlyList<CatalogRelationship> Relationships,
    IReadOnlyList<CatalogViewpoint> Viewpoints,
    IReadOnlyList<string> Diagnostics);

public sealed class CatalogEntity
{
    public string Id { get; set; } = "";
    public CatalogEntityType Type { get; set; }
    public string Name { get; set; } = "";
    public string? Title { get; set; }
    public string Description { get; set; } = "";
    public OwnerDefinition Owner { get; set; } = new();
    public List<string> Tags { get; set; } = [];
    public string? Status { get; set; }
    public string? Criticality { get; set; }
    public string? Classification { get; set; }
    public string? Domain { get; set; }
    public string? Boundary { get; set; }
    public string? SourceOfTruth { get; set; }
}

public sealed class CatalogRelationship
{
    public string Id { get; set; } = "";
    public CatalogRelationshipType Type { get; set; }
    public string From { get; set; } = "";
    public string To { get; set; } = "";
    public string Description { get; set; } = "";
    public string? Direction { get; set; }
    public string? Criticality { get; set; }
    public string? Technology { get; set; }
}

public sealed class CatalogViewpoint
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public List<CatalogEntityType> IncludeEntityTypes { get; set; } = [];
    public List<CatalogRelationshipType> IncludeRelationshipTypes { get; set; } = [];
    public string? Layout { get; set; }
}

public enum CatalogEntityType
{
    Unknown = 0,
    Database,
    Schema,
    Table,
    Column,
    System,
    Interface,
    Pipeline,
    Dataset,
    Domain,
    Consumer,
    Actor,
    Container,
    Component,
    ExternalVendor,
    DataProduct
}

public enum CatalogRelationshipType
{
    Unknown = 0,
    DependsOn,
    ReadsFrom,
    WritesTo,
    PublishesTo,
    OwnedBy,
    BelongsTo,
    Implements,
    Exposes,
    IngestsFrom,
    Feeds,
    Uses,
    SyncsTo,
    Contains,
    MapsTo
}

public static class CatalogViewpointService
{
    public static CatalogGraph Filter(CatalogGraph graph, CatalogViewpoint viewpoint)
    {
        var selectedEntities = graph.Entities
            .Where(e => viewpoint.IncludeEntityTypes.Count == 0 || viewpoint.IncludeEntityTypes.Contains(e.Type))
            .OrderBy(e => e.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var entityIds = selectedEntities
            .Select(e => e.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var selectedRelationships = graph.Relationships
            .Where(r => viewpoint.IncludeRelationshipTypes.Count == 0 || viewpoint.IncludeRelationshipTypes.Contains(r.Type))
            .Where(r => entityIds.Contains(r.From) && entityIds.Contains(r.To))
            .OrderBy(r => r.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new CatalogGraph(selectedEntities, selectedRelationships, [viewpoint], graph.Diagnostics);
    }
}
