
using GitCatalog.Core;
using GitCatalog.Generation;
using GitCatalog.Governance;
using GitCatalog.Import;
using GitCatalog.Serialization;
using GitCatalog.Validation;

namespace GitCatalog.Tests;

public class CatalogTests
{
    [Fact]
    public void Load_Reads_Table_Metadata_From_Yaml()
    {
        var root = CreateTempRepoWithCatalog(
            "sales.order.yaml",
            """
            id: sales.order
            database: sales
            schema: dbo
            description: Customer order table
            owner:
              team: Commercial Systems
            columns:
              - name: OrderId
                type: bigint
                pk: true
                description: Primary key
            """);

        var result = CatalogLoader.Load(root);

        Assert.Empty(result.Diagnostics);
        var table = Assert.Single(result.Tables);
        Assert.Equal("sales.order", table.Id);
        Assert.Equal("Commercial Systems", table.Owner.Team);
        var column = Assert.Single(table.Columns);
        Assert.True(column.Pk);
    }

    [Fact]
    public void Validate_Flags_Missing_Required_Fields()
    {
        var issues = CatalogValidator.Validate(
            [
                new TableDefinition
                {
                    Id = "sales.order",
                    Database = "sales",
                    Schema = "dbo",
                    Description = "",
                    Columns = [new ColumnDefinition { Name = "", Type = "" }]
                }
            ]).ToList();

        Assert.Contains(issues, x => x.Contains("Description is required"));
        Assert.Contains(issues, x => x.Contains("Column name is required"));
        Assert.Contains(issues, x => x.Contains("Column type is required"));
    }

    [Fact]
    public void Lint_Flags_Missing_Owner_And_Column_Descriptions()
    {
        var warnings = GovernanceEngine.Lint(
            [
                new TableDefinition
                {
                    Id = "sales.order",
                    Database = "sales",
                    Schema = "dbo",
                    Description = "short",
                    Owner = new OwnerDefinition { Team = "" },
                    Columns = [new ColumnDefinition { Name = "OrderId", Type = "bigint", Description = "" }]
                }
            ]).ToList();

        Assert.Contains(warnings, x => x.Contains("Missing owner team"));
        Assert.Contains(warnings, x => x.Contains("Description is too short"));
        Assert.Contains(warnings, x => x.Contains("Missing description for column"));
    }

    [Fact]
    public void GenerateEr_Emits_Entities_And_Relationships()
    {
        var er = MermaidGenerator.GenerateEr(
            [
                new TableDefinition
                {
                    Id = "sales.order",
                    Columns =
                    [
                        new ColumnDefinition { Name = "OrderId", Type = "bigint", Pk = true },
                        new ColumnDefinition { Name = "CustomerId", Type = "bigint", Fk = "sales.customer.CustomerId" }
                    ]
                },
                new TableDefinition
                {
                    Id = "sales.customer",
                    Columns = [new ColumnDefinition { Name = "CustomerId", Type = "bigint", Pk = true }]
                }
            ]);

        Assert.Contains("erDiagram", er);
        Assert.Contains("sales_order", er);
        Assert.Contains("bigint OrderId PK", er);
        Assert.Contains("sales_order }o--|| sales_customer : CustomerId", er);
    }

    [Fact]
    public void GenerateCatalogDocs_Emits_Index_And_Table_Page()
    {
        var docs = MarkdownGenerator.GenerateCatalogDocs(
            [
                new TableDefinition
                {
                    Id = "sales.order",
                    Database = "sales",
                    Schema = "dbo",
                    Description = "Customer order table",
                    Owner = new OwnerDefinition { Team = "Commercial Systems" },
                    Columns =
                    [
                        new ColumnDefinition
                        {
                            Name = "OrderId",
                            Type = "bigint",
                            Pk = true,
                            Description = "Primary key"
                        }
                    ]
                }
            ],
            ["WARN: Missing owner team on table 'sales.customer'."]);

        var index = docs.Single(d => d.RelativePath == "index.md").Content;
        var tablePage = docs.Single(d => d.RelativePath == "tables/sales.order.md").Content;

        Assert.Contains("# GitCatalog Generated Documentation", index);
        Assert.Contains("[sales.order](tables/sales.order.md)", index);
        Assert.Contains("Governance Findings", index);
        Assert.Contains("# sales.order", tablePage);
        Assert.Contains("| Name | Type | PK | FK | Description |", tablePage);
        Assert.Contains("| OrderId | bigint | Yes |  | Primary key |", tablePage);
    }

    [Fact]
    public void GenerateSiteAssets_Emits_Manifest_And_App_Files()
    {
        var assets = StaticSiteGenerator.GenerateSiteAssets(
            [
                new TableDefinition { Id = "sales.order" },
                new TableDefinition { Id = "sales.customer" }
            ]);

        Assert.Contains(assets, a => a.RelativePath == "index.html");
        Assert.Contains(assets, a => a.RelativePath == "app.css");
        Assert.Contains(assets, a => a.RelativePath == "app.js");

        var manifest = assets.Single(a => a.RelativePath == "manifest.json").Content;
        Assert.Contains("\"generatedRoot\": \"../generated\"", manifest);
        Assert.Contains("\"sales.customer\"", manifest);
        Assert.Contains("\"sales.order\"", manifest);
    }

    [Fact]
    public void SqlImporter_BuildTables_Maps_Primary_And_Foreign_Keys()
    {
        var importer = new SqlServerImporter();

        var tables = new[]
        {
            new SqlTableRow("sales", "dbo", "order"),
            new SqlTableRow("sales", "dbo", "customer")
        };

        var columns = new[]
        {
            new SqlColumnRow("dbo", "order", "OrderId", "bigint", 1, true),
            new SqlColumnRow("dbo", "order", "CustomerId", "bigint", 2, false),
            new SqlColumnRow("dbo", "customer", "CustomerId", "bigint", 1, true)
        };

        var foreignKeys = new[]
        {
            new SqlForeignKeyRow("dbo", "order", "CustomerId", "dbo", "customer", "CustomerId")
        };

        var mapped = importer.BuildTables(tables, columns, foreignKeys);

        Assert.Equal(2, mapped.Count);
        var order = mapped.Single(t => t.Id == "sales.order");
        Assert.Equal("sales", order.Database);
        Assert.Equal("dbo", order.Schema);
        Assert.Contains(order.Columns, c => c.Name == "OrderId" && c.Pk);
        Assert.Contains(order.Columns, c => c.Name == "CustomerId" && c.Fk == "sales.dbo.customer.CustomerId");
    }

    [Fact]
    public void SqlImporter_MergeWithExisting_Preserves_Table_And_Column_Metadata()
    {
        var importer = new SqlServerImporter();

        var imported = new TableDefinition
        {
            Id = "sales.order",
            Database = "sales",
            Schema = "dbo",
            Description = "Imported from SQL Server table dbo.order",
            Owner = new OwnerDefinition { Team = "" },
            Columns =
            [
                new ColumnDefinition { Name = "OrderId", Type = "bigint", Pk = true, Description = "" },
                new ColumnDefinition { Name = "CustomerId", Type = "bigint", Fk = "sales.dbo.customer.CustomerId", Description = "" }
            ]
        };

        var existing = new TableDefinition
        {
            Id = "sales.order",
            Database = "sales",
            Schema = "dbo",
            Description = "Canonical order table for revenue reporting",
            Owner = new OwnerDefinition { Team = "Commercial Systems" },
            Columns =
            [
                new ColumnDefinition { Name = "OrderId", Type = "bigint", Pk = true, Description = "Stable business key" },
                new ColumnDefinition { Name = "CustomerId", Type = "bigint", Fk = "sales.dbo.customer.CustomerId", Description = "Joins to customer" }
            ]
        };

        var merged = importer.MergeWithExisting(imported, existing);

        Assert.Equal("Canonical order table for revenue reporting", merged.Description);
        Assert.Equal("Commercial Systems", merged.Owner.Team);
        Assert.Contains(merged.Columns, c => c.Name == "OrderId" && c.Description == "Stable business key");
        Assert.Contains(merged.Columns, c => c.Name == "CustomerId" && c.Description == "Joins to customer");
    }

    [Fact]
    public void SqlImporter_MergeWithExisting_Uses_Imported_Metadata_When_Existing_Is_Empty()
    {
        var importer = new SqlServerImporter();

        var imported = new TableDefinition
        {
            Id = "sales.customer",
            Database = "sales",
            Schema = "dbo",
            Description = "Imported from SQL Server table dbo.customer",
            Owner = new OwnerDefinition { Team = "Data Engineering" },
            Columns =
            [
                new ColumnDefinition { Name = "CustomerId", Type = "bigint", Pk = true, Description = "Generated identifier" }
            ]
        };

        var existing = new TableDefinition
        {
            Id = "sales.customer",
            Database = "sales",
            Schema = "dbo",
            Description = "",
            Owner = new OwnerDefinition { Team = "" },
            Columns = [new ColumnDefinition { Name = "CustomerId", Type = "bigint", Pk = true, Description = "" }]
        };

        var merged = importer.MergeWithExisting(imported, existing);

        Assert.Equal("Imported from SQL Server table dbo.customer", merged.Description);
        Assert.Equal("Data Engineering", merged.Owner.Team);
        Assert.Contains(merged.Columns, c => c.Name == "CustomerId" && c.Description == "Generated identifier");
    }

    private static string CreateTempRepoWithCatalog(string fileName, string yaml)
    {
        var root = Path.Combine(Path.GetTempPath(), "gitcatalog-tests", Guid.NewGuid().ToString("N"));
        var tablesPath = Path.Combine(root, "catalog", "tables");
        Directory.CreateDirectory(tablesPath);
        File.WriteAllText(Path.Combine(tablesPath, fileName), yaml);
        return root;
    }
}
