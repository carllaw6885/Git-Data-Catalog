using GitCatalog.Core;
using System.Text;

namespace GitCatalog.Generation;

public static class DomainDependencyGenerator
{
    public static GeneratedAsset Generate(CatalogGraph graph)
    {
        var domains = graph.Entities
            .Where(e => e.Type == CatalogEntityType.Domain)
            .OrderBy(e => e.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var domainIds = domains.Select(d => d.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var entityToDomain = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entity in graph.Entities)
        {
            if (entity.Type == CatalogEntityType.Domain)
            {
                entityToDomain[entity.Id] = entity.Id;
                continue;
            }

            if (!string.IsNullOrWhiteSpace(entity.Domain))
            {
                if (domainIds.Contains(entity.Domain))
                {
                    entityToDomain[entity.Id] = entity.Domain;
                    continue;
                }

                var resolved = domains.FirstOrDefault(d => d.Id.Equals(entity.Domain, StringComparison.OrdinalIgnoreCase));
                if (resolved is not null)
                {
                    entityToDomain[entity.Id] = resolved.Id;
                }
            }
        }

        foreach (var rel in graph.Relationships.Where(r => r.Type == CatalogRelationshipType.BelongsTo))
        {
            if (domainIds.Contains(rel.To) && !entityToDomain.ContainsKey(rel.From))
            {
                entityToDomain[rel.From] = rel.To;
            }
        }

        var edges = new Dictionary<(string From, string To), int>();
        foreach (var rel in graph.Relationships)
        {
            if (!entityToDomain.TryGetValue(rel.From, out var fromDomain)
                || !entityToDomain.TryGetValue(rel.To, out var toDomain)
                || fromDomain.Equals(toDomain, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var key = (fromDomain, toDomain);
            edges[key] = edges.GetValueOrDefault(key) + 1;
        }

        var sb = new StringBuilder();
        sb.AppendLine("flowchart LR");

        var nodeMap = domains
            .Select((domain, index) => (domain, index))
            .ToDictionary(x => x.domain.Id, x => $"D{x.index}", StringComparer.OrdinalIgnoreCase);

        foreach (var domain in domains)
        {
            var nodeId = nodeMap[domain.Id];
            var label = string.IsNullOrWhiteSpace(domain.Title) ? domain.Name : domain.Title;
            if (string.IsNullOrWhiteSpace(label))
            {
                label = domain.Id;
            }

            sb.AppendLine($"  {nodeId}[\"{label.Replace("\"", "\\\"", StringComparison.Ordinal)}\"]");
        }

        foreach (var edge in edges.OrderBy(e => e.Key.From, StringComparer.OrdinalIgnoreCase).ThenBy(e => e.Key.To, StringComparer.OrdinalIgnoreCase))
        {
            var from = nodeMap[edge.Key.From];
            var to = nodeMap[edge.Key.To];
            sb.AppendLine($"  {from} -->|depends_on ({edge.Value})| {to}");
        }

        return new GeneratedAsset("domain/domain-dependencies.mmd", sb.ToString());
    }
}
