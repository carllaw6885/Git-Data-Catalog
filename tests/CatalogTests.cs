
using GitCatalog.Core;
using GitCatalog.Generation;
using GitCatalog.Governance;
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

    private static string CreateTempRepoWithCatalog(string fileName, string yaml)
    {
        var root = Path.Combine(Path.GetTempPath(), "gitcatalog-tests", Guid.NewGuid().ToString("N"));
        var tablesPath = Path.Combine(root, "catalog", "tables");
        Directory.CreateDirectory(tablesPath);
        File.WriteAllText(Path.Combine(tablesPath, fileName), yaml);
        return root;
    }
}
