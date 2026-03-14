using GitCatalog.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace GitCatalog.Serialization;

public static class CatalogGraphLoader
{
    public static CatalogGraph Load(string repoRoot)
    {
        var diagnostics = new List<string>();
        var entities = new List<CatalogEntity>();
        var relationships = new List<CatalogRelationship>();
        var viewpoints = new List<CatalogViewpoint>();

        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        LoadEntities(repoRoot, deserializer, entities, diagnostics);
        LoadRelationships(repoRoot, deserializer, relationships, diagnostics);
        LoadViewpoints(repoRoot, deserializer, viewpoints, diagnostics);
        NormalizeTables(repoRoot, entities, relationships, diagnostics);

        return new CatalogGraph(
            entities.OrderBy(e => e.Id, StringComparer.OrdinalIgnoreCase).ToList(),
            relationships.OrderBy(r => r.Id, StringComparer.OrdinalIgnoreCase).ToList(),
            viewpoints.OrderBy(v => v.Id, StringComparer.OrdinalIgnoreCase).ToList(),
            diagnostics);
    }

    private static void LoadEntities(string repoRoot, IDeserializer deserializer, List<CatalogEntity> entities, List<string> diagnostics)
    {
        var basePath = Path.Combine(repoRoot, "catalog", "entities");
        if (!Directory.Exists(basePath))
        {
            diagnostics.Add($"Architecture entities directory not found: {basePath}");
            return;
        }

        foreach (var file in Directory.GetFiles(basePath, "*.yaml", SearchOption.AllDirectories).OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                var raw = deserializer.Deserialize<EntityYaml>(File.ReadAllText(file));
                if (raw is null)
                {
                    diagnostics.Add($"Failed to deserialize entity file: {file}");
                    continue;
                }

                entities.Add(new CatalogEntity
                {
                    Id = raw.Id ?? "",
                    Type = ParseEntityType(raw.Type),
                    Name = raw.Name ?? raw.Title ?? raw.Id ?? "",
                    Title = raw.Title,
                    Description = raw.Description ?? "",
                    Owner = new OwnerDefinition { Team = raw.Owner?.Team ?? "" },
                    Tags = raw.Tags ?? [],
                    Status = raw.Status,
                    Criticality = raw.Criticality,
                    Classification = raw.Classification,
                    Domain = raw.Domain,
                    Boundary = raw.Boundary,
                    SourceOfTruth = raw.SourceOfTruth,
                    Container = raw.Container,
                    Technology = ParseTechnology(raw.Technology),
                    Kind = raw.Kind
                });
            }
            catch (Exception ex)
            {
                diagnostics.Add($"YAML parse error in {file}: {ex.Message}");
            }
        }
    }

    private static void LoadRelationships(string repoRoot, IDeserializer deserializer, List<CatalogRelationship> relationships, List<string> diagnostics)
    {
        var basePath = Path.Combine(repoRoot, "catalog", "relationships");
        if (!Directory.Exists(basePath))
        {
            diagnostics.Add($"Architecture relationships directory not found: {basePath}");
            return;
        }

        foreach (var file in Directory.GetFiles(basePath, "*.yaml", SearchOption.AllDirectories).OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                var raw = deserializer.Deserialize<RelationshipYaml>(File.ReadAllText(file));
                if (raw is null)
                {
                    diagnostics.Add($"Failed to deserialize relationship file: {file}");
                    continue;
                }

                relationships.Add(new CatalogRelationship
                {
                    Id = raw.Id ?? "",
                    Type = ParseRelationshipType(raw.Type),
                    From = raw.From ?? "",
                    To = raw.To ?? "",
                    Description = raw.Description ?? "",
                    Direction = raw.Direction,
                    Criticality = raw.Criticality,
                    Technology = ParseTechnology(raw.Technology)
                });
            }
            catch (Exception ex)
            {
                diagnostics.Add($"YAML parse error in {file}: {ex.Message}");
            }
        }
    }

    private static void LoadViewpoints(string repoRoot, IDeserializer deserializer, List<CatalogViewpoint> viewpoints, List<string> diagnostics)
    {
        var basePath = Path.Combine(repoRoot, "catalog", "viewpoints");
        if (!Directory.Exists(basePath))
        {
            diagnostics.Add($"Architecture viewpoints directory not found: {basePath}");
            return;
        }

        foreach (var file in Directory.GetFiles(basePath, "*.yaml", SearchOption.AllDirectories).OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                var raw = deserializer.Deserialize<ViewpointYaml>(File.ReadAllText(file));
                if (raw is null)
                {
                    diagnostics.Add($"Failed to deserialize viewpoint file: {file}");
                    continue;
                }

                viewpoints.Add(new CatalogViewpoint
                {
                    Id = raw.Id ?? "",
                    Name = raw.Name ?? raw.Id ?? "",
                    Description = raw.Description ?? "",
                    IncludeEntityTypes = (raw.IncludeEntityTypes ?? []).Select(ParseEntityType).ToList(),
                    IncludeRelationshipTypes = (raw.IncludeRelationshipTypes ?? []).Select(ParseRelationshipType).ToList(),
                    Layout = raw.Layout
                });
            }
            catch (Exception ex)
            {
                diagnostics.Add($"YAML parse error in {file}: {ex.Message}");
            }
        }
    }

    private static void NormalizeTables(string repoRoot, List<CatalogEntity> entities, List<CatalogRelationship> relationships, List<string> diagnostics)
    {
        var tableLoad = CatalogLoader.Load(repoRoot);
        foreach (var diagnostic in tableLoad.Diagnostics)
        {
            if (diagnostic.Contains("Catalog tables directory not found", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            diagnostics.Add(diagnostic);
        }

        foreach (var table in tableLoad.Tables)
        {
            entities.Add(new CatalogEntity
            {
                Id = table.Id,
                Type = CatalogEntityType.Table,
                Name = table.Id,
                Title = table.Id,
                Description = table.Description,
                Owner = table.Owner,
                Domain = table.Database,
                SourceOfTruth = "catalog/tables"
            });

            foreach (var column in table.Columns.OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase))
            {
                var columnId = $"{table.Id}.{column.Name}";
                entities.Add(new CatalogEntity
                {
                    Id = columnId,
                    Type = CatalogEntityType.Column,
                    Name = column.Name,
                    Title = column.Name,
                    Description = column.Description,
                    Owner = table.Owner,
                    Domain = table.Database,
                    SourceOfTruth = "catalog/tables"
                });

                relationships.Add(new CatalogRelationship
                {
                    Id = $"rel.contains.{NormalizeId(table.Id)}.{NormalizeId(column.Name)}",
                    Type = CatalogRelationshipType.Contains,
                    From = table.Id,
                    To = columnId,
                    Description = "Column belongs to table",
                    Direction = "unidirectional"
                });

                if (!string.IsNullOrWhiteSpace(column.Fk))
                {
                    relationships.Add(new CatalogRelationship
                    {
                        Id = $"rel.reads-from.{NormalizeId(table.Id)}.{NormalizeId(column.Name)}",
                        Type = CatalogRelationshipType.ReadsFrom,
                        From = columnId,
                        To = column.Fk!,
                        Description = "Foreign key dependency",
                        Direction = "unidirectional"
                    });
                }
            }
        }
    }

    private static string NormalizeId(string id)
        => id.Replace('.', '-').Replace(' ', '-').ToLowerInvariant();

    private static CatalogEntityType ParseEntityType(string? value)
    {
        var normalized = NormalizeToken(value);
        return normalized switch
        {
            "database" => CatalogEntityType.Database,
            "schema" => CatalogEntityType.Schema,
            "table" => CatalogEntityType.Table,
            "column" => CatalogEntityType.Column,
            "system" => CatalogEntityType.System,
            "interface" => CatalogEntityType.Interface,
            "pipeline" => CatalogEntityType.Pipeline,
            "dataset" => CatalogEntityType.Dataset,
            "domain" => CatalogEntityType.Domain,
            "consumer" => CatalogEntityType.Consumer,
            "actor" => CatalogEntityType.Actor,
            "container" => CatalogEntityType.Container,
            "component" => CatalogEntityType.Component,
            "externalvendor" => CatalogEntityType.ExternalVendor,
            "dataproduct" => CatalogEntityType.DataProduct,
            _ => CatalogEntityType.Unknown
        };
    }

    private static CatalogRelationshipType ParseRelationshipType(string? value)
    {
        var normalized = NormalizeToken(value);
        return normalized switch
        {
            "dependson" => CatalogRelationshipType.DependsOn,
            "readsfrom" => CatalogRelationshipType.ReadsFrom,
            "writesto" => CatalogRelationshipType.WritesTo,
            "publishesto" => CatalogRelationshipType.PublishesTo,
            "ownedby" => CatalogRelationshipType.OwnedBy,
            "belongsto" => CatalogRelationshipType.BelongsTo,
            "implements" => CatalogRelationshipType.Implements,
            "exposes" => CatalogRelationshipType.Exposes,
            "ingestsfrom" => CatalogRelationshipType.IngestsFrom,
            "feeds" => CatalogRelationshipType.Feeds,
            "uses" => CatalogRelationshipType.Uses,
            "syncsto" => CatalogRelationshipType.SyncsTo,
            "contains" => CatalogRelationshipType.Contains,
            "mapsto" => CatalogRelationshipType.MapsTo,
            _ => CatalogRelationshipType.Unknown
        };
    }

    private static string NormalizeToken(string? value)
        => (value ?? "").Replace("_", "", StringComparison.Ordinal).Replace("-", "", StringComparison.Ordinal).Trim().ToLowerInvariant();

    private static string? ParseTechnology(object? rawTechnology)
    {
        if (rawTechnology is null)
        {
            return null;
        }

        if (rawTechnology is string scalar)
        {
            return scalar;
        }

        if (rawTechnology is Dictionary<object, object> map)
        {
            if (map.TryGetValue("type", out var type) && type is not null)
            {
                return type.ToString();
            }

            return string.Join(",", map.Select(kv => $"{kv.Key}:{kv.Value}").OrderBy(x => x, StringComparer.OrdinalIgnoreCase));
        }

        return rawTechnology.ToString();
    }

    private sealed class EntityYaml
    {
        public string? Id { get; set; }
        public string? Type { get; set; }
        public string? Name { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }
        public OwnerYaml? Owner { get; set; }
        public List<string>? Tags { get; set; }
        public string? Status { get; set; }
        public string? Criticality { get; set; }
        public string? Classification { get; set; }
        public string? Domain { get; set; }
        public string? Boundary { get; set; }
        public string? SourceOfTruth { get; set; }
        public string? Container { get; set; }
        public object? Technology { get; set; }
        public string? Kind { get; set; }
    }

    private sealed class RelationshipYaml
    {
        public string? Id { get; set; }
        public string? Type { get; set; }
        public string? From { get; set; }
        public string? To { get; set; }
        public string? Description { get; set; }
        public string? Direction { get; set; }
        public string? Criticality { get; set; }
        public object? Technology { get; set; }
    }

    private sealed class ViewpointYaml
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public List<string>? IncludeEntityTypes { get; set; }
        public List<string>? IncludeRelationshipTypes { get; set; }
        public string? Layout { get; set; }
    }

    private sealed class OwnerYaml
    {
        public string? Team { get; set; }
    }
}
