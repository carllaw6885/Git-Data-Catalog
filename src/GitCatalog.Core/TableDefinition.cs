
namespace GitCatalog.Core;

public sealed class TableDefinition
{
    public string Id { get; set; } = "";
    public string Database { get; set; } = "";
    public string Schema { get; set; } = "";
    public string Description { get; set; } = "";
    public OwnerDefinition Owner { get; set; } = new();
    public List<ColumnDefinition> Columns { get; set; } = [];
}

public sealed class OwnerDefinition
{
    public string Team { get; set; } = "";
}

public sealed class ColumnDefinition
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public bool Pk { get; set; }
    public string? Fk { get; set; }
    public string Description { get; set; } = "";
}
