
using GitCatalog.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace GitCatalog.Serialization;

public static class CatalogLoader
{
    public static CatalogLoadResult Load(string repoRoot)
    {
        var diagnostics = new List<string>();
        var tables = new List<TableDefinition>();

        var catalogPath = Path.Combine(repoRoot, "catalog", "tables");
        if (!Directory.Exists(catalogPath))
        {
            diagnostics.Add($"Catalog tables directory not found: {catalogPath}");
            return new CatalogLoadResult(tables, diagnostics);
        }

        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        foreach (var file in Directory.GetFiles(catalogPath, "*.yaml", SearchOption.AllDirectories))
        {
            try
            {
                var text = File.ReadAllText(file);
                var table = deserializer.Deserialize<TableDefinition>(text);

                if (table is null)
                {
                    diagnostics.Add($"Failed to deserialize table file: {file}");
                    continue;
                }

                tables.Add(table);
            }
            catch (Exception ex)
            {
                diagnostics.Add($"YAML parse error in {file}: {ex.Message}");
            }
        }

        return new CatalogLoadResult(tables, diagnostics);
    }
}

public sealed record CatalogLoadResult(IReadOnlyList<TableDefinition> Tables, IReadOnlyList<string> Diagnostics);
