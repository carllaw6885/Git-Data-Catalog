using GitCatalog.Core;

namespace GitCatalog.Validation;

public static class CatalogGraphValidator
{
    public static IEnumerable<string> Validate(CatalogGraph graph)
    {
        var entityById = new Dictionary<string, CatalogEntity>(StringComparer.OrdinalIgnoreCase);

        foreach (var entity in graph.Entities)
        {
            if (string.IsNullOrWhiteSpace(entity.Id))
            {
                yield return "Graph entity ID is required.";
                continue;
            }

            if (entity.Type == CatalogEntityType.Unknown)
            {
                yield return $"Graph entity '{entity.Id}' has unsupported type.";
            }

            if (!entityById.TryAdd(entity.Id, entity))
            {
                yield return $"Duplicate graph entity ID found: {entity.Id}";
            }
        }

        var relationshipSeen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var relationship in graph.Relationships)
        {
            if (string.IsNullOrWhiteSpace(relationship.Id))
            {
                yield return "Graph relationship ID is required.";
            }
            else if (!relationshipSeen.Add(relationship.Id))
            {
                yield return $"Duplicate graph relationship ID found: {relationship.Id}";
            }

            if (relationship.Type == CatalogRelationshipType.Unknown)
            {
                yield return $"Graph relationship '{relationship.Id}' has unsupported type.";
            }

            if (string.IsNullOrWhiteSpace(relationship.From) || !entityById.ContainsKey(relationship.From))
            {
                yield return $"Graph relationship '{relationship.Id}' has unknown source entity '{relationship.From}'.";
            }

            if (string.IsNullOrWhiteSpace(relationship.To) || !entityById.ContainsKey(relationship.To))
            {
                yield return $"Graph relationship '{relationship.Id}' has unknown target entity '{relationship.To}'.";
            }
        }

        var viewpointSeen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var viewpoint in graph.Viewpoints)
        {
            if (string.IsNullOrWhiteSpace(viewpoint.Id))
            {
                yield return "Graph viewpoint ID is required.";
                continue;
            }

            if (!viewpointSeen.Add(viewpoint.Id))
            {
                yield return $"Duplicate graph viewpoint ID found: {viewpoint.Id}";
            }

            if (viewpoint.IncludeEntityTypes.Any(t => t == CatalogEntityType.Unknown))
            {
                yield return $"Viewpoint '{viewpoint.Id}' includes unsupported entity type.";
            }

            if (viewpoint.IncludeRelationshipTypes.Any(t => t == CatalogRelationshipType.Unknown))
            {
                yield return $"Viewpoint '{viewpoint.Id}' includes unsupported relationship type.";
            }
        }
    }
}
