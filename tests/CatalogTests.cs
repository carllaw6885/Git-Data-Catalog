
using GitCatalog.Core;
using GitCatalog.Cli;
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
        public void Load_Orders_Tables_Deterministically()
        {
                var root = Path.Combine(Path.GetTempPath(), "gitcatalog-tests", Guid.NewGuid().ToString("N"));
                var tablesPath = Path.Combine(root, "catalog", "tables");
                Directory.CreateDirectory(tablesPath);

                File.WriteAllText(
                        Path.Combine(tablesPath, "zeta.table.yaml"),
                        """
                        id: sales.zeta
                        database: sales
                        schema: dbo
                        description: Zeta table
                        owner:
                          team: Team A
                        columns:
                          - name: Id
                            type: bigint
                            pk: true
                            description: Primary key
                        """);

                File.WriteAllText(
                        Path.Combine(tablesPath, "alpha.table.yaml"),
                        """
                        id: sales.alpha
                        database: sales
                        schema: dbo
                        description: Alpha table
                        owner:
                          team: Team A
                        columns:
                          - name: Id
                            type: bigint
                            pk: true
                            description: Primary key
                        """);

                var result = CatalogLoader.Load(root);

                Assert.Empty(result.Diagnostics);
                Assert.Equal(["sales.alpha", "sales.zeta"], result.Tables.Select(t => t.Id).ToArray());
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
    public void Validate_Flags_Duplicate_Columns_And_Invalid_Foreign_Keys()
    {
        var issues = CatalogValidator.Validate(
            [
                new TableDefinition
                {
                    Id = "sales.order",
                    Database = "sales",
                    Schema = "dbo",
                    Description = "Order table for testing",
                    Columns =
                    [
                        new ColumnDefinition { Name = "OrderId", Type = "bigint", Pk = true },
                        new ColumnDefinition { Name = "OrderId", Type = "bigint" },
                        new ColumnDefinition { Name = "CustomerId", Type = "bigint", Fk = "badformat" },
                        new ColumnDefinition { Name = "RegionId", Type = "bigint", Fk = "sales.region.RegionId" }
                    ]
                },
                new TableDefinition
                {
                    Id = "sales.customer",
                    Database = "sales",
                    Schema = "dbo",
                    Description = "Customer table for testing",
                    Columns = [new ColumnDefinition { Name = "CustomerId", Type = "bigint", Pk = true }]
                }
            ]).ToList();

        Assert.Contains(issues, x => x.Contains("Duplicate column name 'OrderId'"));
        Assert.Contains(issues, x => x.Contains("Invalid foreign key format 'badformat'"));
        Assert.Contains(issues, x => x.Contains("Foreign key target table 'sales.region' not found"));
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
    public void Lint_Flags_AutoGenerated_Description_And_Missing_PrimaryKey()
    {
        var warnings = GovernanceEngine.Lint(
            [
                new TableDefinition
                {
                    Id = "sales.stg.order",
                    Database = "sales",
                    Schema = "stg",
                    Description = "Imported from SQL Server table stg.order",
                    Owner = new OwnerDefinition { Team = "Platform" },
                    Columns =
                    [
                        new ColumnDefinition
                        {
                            Name = "OrderId",
                            Type = "bigint",
                            Pk = false,
                            Description = "Imported from SQL Server column"
                        }
                    ]
                }
            ]).ToList();

        Assert.Contains(warnings, x => x.Contains("auto-generated description"));
        Assert.Contains(warnings, x => x.Contains("has no primary key"));
        Assert.Contains(warnings, x => x.Contains("Column 'sales.stg.order.OrderId' is using an auto-generated description"));
    }

    [Fact]
    public void Lint_Uses_Configurable_Policy_For_Enable_And_Severity()
    {
        var policy = new GovernancePolicy
        {
            MissingOwnerTeam = new GovernanceRulePolicy { Enabled = false, Severity = "warn" },
            MissingColumnDescription = new GovernanceRulePolicy { Enabled = true, Severity = "error" },
            MissingPrimaryKey = new GovernanceRulePolicy { Enabled = true, Severity = "warn" },
            ShortTableDescription = new GovernanceRulePolicy { Enabled = false, Severity = "warn", MinLength = 15 },
            AutoGeneratedTableDescription = new GovernanceRulePolicy { Enabled = false, Severity = "warn" },
            AutoGeneratedColumnDescription = new GovernanceRulePolicy { Enabled = false, Severity = "warn" }
        };

        var warnings = GovernanceEngine.Lint(
            [
                new TableDefinition
                {
                    Id = "sales.order",
                    Database = "sales",
                    Schema = "dbo",
                    Description = "short",
                    Owner = new OwnerDefinition { Team = "" },
                    Columns = [new ColumnDefinition { Name = "OrderId", Type = "bigint", Description = "", Pk = false }]
                }
            ],
            policy).ToList();

        Assert.DoesNotContain(warnings, w => w.Contains("Missing owner team"));
        Assert.Contains(warnings, w => w.StartsWith("ERROR:") && w.Contains("Missing description for column"));
        Assert.Contains(warnings, w => w.StartsWith("WARN:") && w.Contains("has no primary key"));
    }

    [Fact]
    public void GovernancePolicyLoader_Loads_File_From_Catalog_Governance_Path()
    {
        var root = Path.Combine(Path.GetTempPath(), "gitcatalog-tests", Guid.NewGuid().ToString("N"));
        var policyPath = Path.Combine(root, "catalog", "governance");
        Directory.CreateDirectory(policyPath);
        File.WriteAllText(
            Path.Combine(policyPath, "policy.yaml"),
            """
            missingOwnerTeam:
              enabled: false
              severity: info
            missingColumnDescription:
              enabled: true
              severity: error
            """);

        var result = GovernancePolicyLoader.Load(root);

        Assert.Empty(result.Diagnostics);
        Assert.False(result.Policy.MissingOwnerTeam.Enabled);
        Assert.Equal("error", result.Policy.MissingColumnDescription.Severity);
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
    public void GenerateGraphView_Emits_Flowchart_From_Viewpoint_Filter()
    {
        var graph = new CatalogGraph(
            [
                new CatalogEntity { Id = "crm", Type = CatalogEntityType.System, Name = "CRM" },
                new CatalogEntity { Id = "sales", Type = CatalogEntityType.Domain, Name = "Sales" },
                new CatalogEntity { Id = "orders", Type = CatalogEntityType.Table, Name = "Orders" }
            ],
            [
                new CatalogRelationship { Id = "rel1", Type = CatalogRelationshipType.BelongsTo, From = "crm", To = "sales" },
                new CatalogRelationship { Id = "rel2", Type = CatalogRelationshipType.ReadsFrom, From = "crm", To = "orders" }
            ],
            [],
            []);

        var viewpoint = new CatalogViewpoint
        {
            Id = "system-landscape",
            Name = "System Landscape",
            Layout = "LR",
            IncludeEntityTypes = [CatalogEntityType.System, CatalogEntityType.Domain],
            IncludeRelationshipTypes = [CatalogRelationshipType.BelongsTo, CatalogRelationshipType.ReadsFrom]
        };

        var mermaid = MermaidGenerator.GenerateGraphView(graph, viewpoint);

        Assert.Contains("flowchart LR", mermaid);
        Assert.Contains("belongs_to", mermaid);
        Assert.DoesNotContain("reads_from", mermaid);
        Assert.DoesNotContain("Orders", mermaid);
    }

    [Fact]
    public void LineageGenerator_Uses_Lineage_Viewpoints_From_Graph()
    {
        var graph = new CatalogGraph(
            [
                new CatalogEntity { Id = "crm", Type = CatalogEntityType.System, Name = "CRM" },
                new CatalogEntity { Id = "sales.order", Type = CatalogEntityType.Table, Name = "Order" },
                new CatalogEntity { Id = "sales.curated", Type = CatalogEntityType.Dataset, Name = "Curated" }
            ],
            [
                new CatalogRelationship { Id = "rel1", Type = CatalogRelationshipType.IngestsFrom, From = "crm", To = "sales.order" },
                new CatalogRelationship { Id = "rel2", Type = CatalogRelationshipType.Feeds, From = "sales.order", To = "sales.curated" }
            ],
            [
                new CatalogViewpoint
                {
                    Id = "sales-lineage",
                    Name = "Sales Lineage",
                    Layout = "LR",
                    IncludeEntityTypes = [CatalogEntityType.System, CatalogEntityType.Table, CatalogEntityType.Dataset],
                    IncludeRelationshipTypes = [CatalogRelationshipType.IngestsFrom, CatalogRelationshipType.Feeds]
                }
            ],
            []);

        var assets = LineageGenerator.Generate(graph);

        var lineage = Assert.Single(assets);
        Assert.Equal("lineage/sales-lineage.mmd", lineage.RelativePath);
        Assert.Contains("flowchart LR", lineage.Content);
        Assert.Contains("ingests_from", lineage.Content);
        Assert.Contains("feeds", lineage.Content);
    }

    [Fact]
    public void DomainDependencyGenerator_Emits_Domain_Dependencies_From_Graph()
    {
        var graph = new CatalogGraph(
            [
                new CatalogEntity { Id = "sales", Type = CatalogEntityType.Domain, Name = "Sales" },
                new CatalogEntity { Id = "finance", Type = CatalogEntityType.Domain, Name = "Finance" },
                new CatalogEntity { Id = "crm", Type = CatalogEntityType.System, Name = "CRM", Domain = "sales" },
                new CatalogEntity { Id = "billing", Type = CatalogEntityType.System, Name = "Billing", Domain = "finance" }
            ],
            [
                new CatalogRelationship { Id = "rel1", Type = CatalogRelationshipType.DependsOn, From = "crm", To = "billing" }
            ],
            [],
            []);

        var diagram = DomainDependencyGenerator.Generate(graph);

        Assert.Equal("domain/domain-dependencies.mmd", diagram.RelativePath);
        Assert.Contains("flowchart LR", diagram.Content);
        Assert.Contains("depends_on (1)", diagram.Content);
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
        Assert.Contains(order.Columns, c => c.Name == "CustomerId" && c.Fk == "sales.customer.CustomerId");
    }

    [Fact]
    public void SqlImporter_BuildTables_Uses_Schema_In_Id_For_NonDbo_Tables()
    {
        var importer = new SqlServerImporter();

        var tables = new[]
        {
            new SqlTableRow("sales", "stg", "order")
        };

        var columns = new[]
        {
            new SqlColumnRow("stg", "order", "OrderId", "bigint", 1, true)
        };

        var mapped = importer.BuildTables(tables, columns, []);

        var table = Assert.Single(mapped);
        Assert.Equal("sales.stg.order", table.Id);
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
                new ColumnDefinition { Name = "CustomerId", Type = "bigint", Fk = "sales.customer.CustomerId", Description = "" }
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
                new ColumnDefinition { Name = "CustomerId", Type = "bigint", Fk = "sales.customer.CustomerId", Description = "Joins to customer" }
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

    [Fact]
    public void SqlImporter_BuildImportPlan_Classifies_Create_Update_Unchanged_And_Remove()
    {
        var importer = new SqlServerImporter();
        var root = Path.Combine(Path.GetTempPath(), "gitcatalog-tests", Guid.NewGuid().ToString("N"));
        var tablesPath = Path.Combine(root, "catalog", "tables");
        Directory.CreateDirectory(tablesPath);

        File.WriteAllText(
            Path.Combine(tablesPath, "sales.order.yaml"),
            """
            id: sales.order
            database: sales
            schema: dbo
            description: Canonical order table
            owner:
              team: Commercial Systems
            columns:
              - name: OrderId
                type: bigint
                pk: true
                description: Primary key
            """);

        File.WriteAllText(
            Path.Combine(tablesPath, "sales.legacy.yaml"),
            """
            id: sales.legacy
            database: sales
            schema: dbo
            description: Legacy table
            owner:
              team: Legacy
            columns:
              - name: LegacyId
                type: bigint
                pk: true
                description: Legacy key
            """);

        var imported = new List<TableDefinition>
        {
            new()
            {
                Id = "sales.customer",
                Database = "sales",
                Schema = "dbo",
                Description = "Imported from SQL Server table dbo.customer",
                Owner = new OwnerDefinition(),
                Columns = [new ColumnDefinition { Name = "CustomerId", Type = "bigint", Pk = true, Description = "" }]
            },
            new()
            {
                Id = "sales.order",
                Database = "sales",
                Schema = "dbo",
                Description = "Imported from SQL Server table dbo.order",
                Owner = new OwnerDefinition(),
                Columns = [new ColumnDefinition { Name = "OrderId", Type = "int", Pk = true, Description = "" }]
            }
        };

        var plan = importer.BuildImportPlan(imported, root);

        Assert.Contains(plan.Changes, c => c.TableId == "sales.customer" && c.Kind == ImportChangeKind.Create);
        Assert.Contains(plan.Changes, c => c.TableId == "sales.order" && c.Kind == ImportChangeKind.Update);
        Assert.Contains(plan.Changes, c => c.TableId == "sales.legacy" && c.Kind == ImportChangeKind.Remove);
        Assert.Equal(2, plan.Writes.Count);

        var update = Assert.Single(plan.Changes, c => c.TableId == "sales.order");
        Assert.Contains(update.DriftDetails, d => d.Contains("Column type changed: OrderId bigint -> int"));

        var create = Assert.Single(plan.Changes, c => c.TableId == "sales.customer");
        Assert.Contains(create.DriftDetails, d => d.Contains("file will be created", StringComparison.OrdinalIgnoreCase));

        var remove = Assert.Single(plan.Changes, c => c.TableId == "sales.legacy");
        Assert.Contains(remove.DriftDetails, d => d.Contains("absent from source schema", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void SqlImporter_BuildImportPlan_Does_Not_Write_Files()
    {
        var importer = new SqlServerImporter();
        var root = Path.Combine(Path.GetTempPath(), "gitcatalog-tests", Guid.NewGuid().ToString("N"));
        var tablesPath = Path.Combine(root, "catalog", "tables");
        Directory.CreateDirectory(tablesPath);

        var imported = new List<TableDefinition>
        {
            new()
            {
                Id = "sales.preview",
                Database = "sales",
                Schema = "dbo",
                Description = "Imported from SQL Server table dbo.preview",
                Owner = new OwnerDefinition(),
                Columns = [new ColumnDefinition { Name = "PreviewId", Type = "bigint", Pk = true, Description = "" }]
            }
        };

        var plan = importer.BuildImportPlan(imported, root);
        Assert.Contains(plan.Changes, c => c.TableId == "sales.preview" && c.Kind == ImportChangeKind.Create);
        var change = Assert.Single(plan.Changes);
        Assert.NotEmpty(change.DriftDetails);
        Assert.False(File.Exists(Path.Combine(tablesPath, "sales.preview.yaml")));
    }

    [Fact]
    public void SqlImporter_BuildImportPlan_Does_Not_Create_Tables_Directory_When_Missing()
    {
        var importer = new SqlServerImporter();
        var root = Path.Combine(Path.GetTempPath(), "gitcatalog-tests", Guid.NewGuid().ToString("N"));
        var tablesPath = Path.Combine(root, "catalog", "tables");

        var imported = new List<TableDefinition>
        {
            new()
            {
                Id = "sales.preview",
                Database = "sales",
                Schema = "dbo",
                Description = "Imported from SQL Server table dbo.preview",
                Owner = new OwnerDefinition(),
                Columns = [new ColumnDefinition { Name = "PreviewId", Type = "bigint", Pk = true, Description = "" }]
            }
        };

        var plan = importer.BuildImportPlan(imported, root);

        Assert.Single(plan.Writes);
        Assert.False(Directory.Exists(tablesPath));
    }

    [Fact]
    public void ImportOptionsParser_Supports_ConnectionEnv_And_RepoRoot()
    {
        Environment.SetEnvironmentVariable("GITCATALOG_TEST_SQL_CONN", "Server=.;Database=sales;User Id=sa;Password=secret;");

        try
        {
            var args = new[] { "import-sqlserver", "--connection-env", "GITCATALOG_TEST_SQL_CONN", "./repo" };
            var parsed = ImportCommandOptionsParser.Parse(args, Directory.GetCurrentDirectory());

            Assert.True(parsed.IsValid);
            Assert.Equal("Server=.;Database=sales;User Id=sa;Password=secret;", parsed.ConnectionString);
            Assert.Equal(Path.GetFullPath("./repo"), parsed.RepoRoot);
            Assert.False(parsed.UsesInlineConnectionString);
        }
        finally
        {
            Environment.SetEnvironmentVariable("GITCATALOG_TEST_SQL_CONN", null);
        }
    }

    [Fact]
    public void ImportOptionsParser_Supports_ConnectionFile()
    {
        var root = Path.Combine(Path.GetTempPath(), "gitcatalog-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var connFile = Path.Combine(root, "conn.txt");
        File.WriteAllText(connFile, "Server=.;Database=sales;User Id=sa;Password=secret;\n");

        var args = new[] { "import-sqlserver", "--connection-file", connFile };
        var parsed = ImportCommandOptionsParser.Parse(args, root);

        Assert.True(parsed.IsValid);
        Assert.Equal("Server=.;Database=sales;User Id=sa;Password=secret;", parsed.ConnectionString);
        Assert.False(parsed.UsesInlineConnectionString);
    }

    [Fact]
    public void ImportOptionsParser_Rejects_Multiple_Connection_Sources()
    {
        var args = new[]
        {
            "import-sqlserver",
            "Server=.;Database=sales;",
            "--connection-env",
            "GITCATALOG_TEST_SQL_CONN"
        };

        var parsed = ImportCommandOptionsParser.Parse(args, Directory.GetCurrentDirectory());

        Assert.False(parsed.IsValid);
        Assert.Contains("Connection source conflict", parsed.Error ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ImportOptionsParser_Rejects_Invalid_Timeout()
    {
        var args = new[] { "import-sqlserver", "--timeout-seconds", "0", "Server=.;Database=sales;" };
        var parsed = ImportCommandOptionsParser.Parse(args, Directory.GetCurrentDirectory());

        Assert.False(parsed.IsValid);
        Assert.Contains("positive integer", parsed.Error ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GraphLoader_Loads_Entities_Relationships_Viewpoints_And_Table_Normalization()
    {
        var root = Path.Combine(Path.GetTempPath(), "gitcatalog-tests", Guid.NewGuid().ToString("N"));

        var systemPath = Path.Combine(root, "catalog", "entities", "systems");
        Directory.CreateDirectory(systemPath);
        File.WriteAllText(
            Path.Combine(systemPath, "crm.yaml"),
            """
            id: crm
            type: system
            name: CRM
            description: Source system
            owner:
              team: Commercial Systems
            """);

        var relationshipsPath = Path.Combine(root, "catalog", "relationships", "integrations");
        Directory.CreateDirectory(relationshipsPath);
        File.WriteAllText(
            Path.Combine(relationshipsPath, "crm-to-table.yaml"),
            """
            id: rel.crm-to-order
            type: ingests_from
            from: crm
            to: sales.order
            description: CRM feeds orders
            criticality: high
            technology:
              type: adf
            """);

        var viewpointsPath = Path.Combine(root, "catalog", "viewpoints", "architecture");
        Directory.CreateDirectory(viewpointsPath);
        File.WriteAllText(
            Path.Combine(viewpointsPath, "landscape.yaml"),
            """
            id: landscape
            name: Landscape
            includeEntityTypes:
              - system
              - table
            includeRelationshipTypes:
              - ingests_from
            """);

        var tablesPath = Path.Combine(root, "catalog", "tables");
        Directory.CreateDirectory(tablesPath);
        File.WriteAllText(
            Path.Combine(tablesPath, "sales.order.yaml"),
            """
            id: sales.order
            database: sales
            schema: dbo
            description: Order table
            owner:
              team: Data
            columns:
              - name: OrderId
                type: bigint
                pk: true
                description: Primary key
            """);

        var graph = CatalogGraphLoader.Load(root);

        Assert.Empty(graph.Diagnostics);
        Assert.Contains(graph.Entities, e => e.Id == "crm" && e.Type == CatalogEntityType.System);
        Assert.Contains(graph.Entities, e => e.Id == "sales.order" && e.Type == CatalogEntityType.Table);
        Assert.Contains(graph.Entities, e => e.Id == "sales.order.OrderId" && e.Type == CatalogEntityType.Column);
        Assert.Contains(graph.Relationships, r => r.Id == "rel.crm-to-order" && r.Type == CatalogRelationshipType.IngestsFrom);
        Assert.Contains(graph.Relationships, r => r.Type == CatalogRelationshipType.Contains && r.From == "sales.order");
        Assert.Contains(graph.Viewpoints, v => v.Id == "landscape");
    }

    [Fact]
    public void GraphLoader_Maps_C4_Metadata_Fields()
    {
        var root = Path.Combine(Path.GetTempPath(), "gitcatalog-tests", Guid.NewGuid().ToString("N"));
        var systemsPath = Path.Combine(root, "catalog", "entities", "systems");
        Directory.CreateDirectory(systemsPath);
        File.WriteAllText(
            Path.Combine(systemsPath, "crm.yaml"),
            """
            id: crm
            type: system
            name: CRM
            description: Source system
            boundary: external
            kind: saas
            technology:
              vendor: Salesforce
            """);

        var componentsPath = Path.Combine(root, "catalog", "entities", "components");
        Directory.CreateDirectory(componentsPath);
        File.WriteAllText(
            Path.Combine(componentsPath, "engine.yaml"),
            """
            id: gc.engine
            type: component
            name: Engine
            description: Internal engine
            container: gitcatalog.cli
            technology:
              type: dotnet
            """);

        Directory.CreateDirectory(Path.Combine(root, "catalog", "relationships", "integrations"));
        Directory.CreateDirectory(Path.Combine(root, "catalog", "viewpoints", "architecture"));

        var graph = CatalogGraphLoader.Load(root);

        Assert.Empty(graph.Diagnostics);
        var crm = Assert.Single(graph.Entities, e => e.Id == "crm");
        Assert.Equal("external", crm.Boundary);
        Assert.Equal("saas", crm.Kind);
        Assert.NotNull(crm.Technology);

        var engine = Assert.Single(graph.Entities, e => e.Id == "gc.engine");
        Assert.Equal("gitcatalog.cli", engine.Container);
        Assert.Equal("dotnet", engine.Technology);
    }

    [Fact]
    public void GraphValidator_Flags_Unknown_Relationship_Endpoints()
    {
        var graph = new CatalogGraph(
            [new CatalogEntity { Id = "crm", Type = CatalogEntityType.System, Name = "CRM" }],
            [new CatalogRelationship { Id = "rel1", Type = CatalogRelationshipType.DependsOn, From = "crm", To = "missing" }],
            [],
            []);

        var issues = CatalogGraphValidator.Validate(graph).ToList();

        Assert.Contains(issues, i => i.Contains("unknown target entity", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ViewpointService_Filters_To_Selected_Types_And_Connected_Edges()
    {
        var graph = new CatalogGraph(
            [
                new CatalogEntity { Id = "crm", Type = CatalogEntityType.System, Name = "CRM" },
                new CatalogEntity { Id = "sales", Type = CatalogEntityType.Domain, Name = "Sales" },
                new CatalogEntity { Id = "orders", Type = CatalogEntityType.Table, Name = "Orders" }
            ],
            [
                new CatalogRelationship { Id = "rel1", Type = CatalogRelationshipType.BelongsTo, From = "crm", To = "sales" },
                new CatalogRelationship { Id = "rel2", Type = CatalogRelationshipType.ReadsFrom, From = "crm", To = "orders" }
            ],
            [],
            []);

        var viewpoint = new CatalogViewpoint
        {
            Id = "arch",
            Name = "Architecture",
            IncludeEntityTypes = [CatalogEntityType.System, CatalogEntityType.Domain],
            IncludeRelationshipTypes = [CatalogRelationshipType.BelongsTo, CatalogRelationshipType.ReadsFrom]
        };

        var filtered = CatalogViewpointService.Filter(graph, viewpoint);

        Assert.Equal(2, filtered.Entities.Count);
        Assert.Contains(filtered.Entities, e => e.Id == "crm");
        Assert.Contains(filtered.Entities, e => e.Id == "sales");
        var rel = Assert.Single(filtered.Relationships);
        Assert.Equal("rel1", rel.Id);
    }

    [Fact]
    public void C4ModelBuilder_Builds_Context_Level_From_Graph()
    {
        var graph = new CatalogGraph(
            [
                new CatalogEntity { Id = "actor.sales", Type = CatalogEntityType.Actor, Name = "Sales Analyst" },
                new CatalogEntity { Id = "crm", Type = CatalogEntityType.System, Name = "CRM" },
                new CatalogEntity { Id = "vendor.sf", Type = CatalogEntityType.ExternalVendor, Name = "Salesforce" },
                new CatalogEntity { Id = "container.api", Type = CatalogEntityType.Container, Name = "API" }
            ],
            [
                new CatalogRelationship { Id = "rel1", Type = CatalogRelationshipType.Uses, From = "actor.sales", To = "crm" },
                new CatalogRelationship { Id = "rel2", Type = CatalogRelationshipType.DependsOn, From = "crm", To = "vendor.sf" },
                new CatalogRelationship { Id = "rel3", Type = CatalogRelationshipType.Contains, From = "crm", To = "container.api" }
            ],
            [],
            []);

        var model = C4ModelBuilder.Build(graph, C4Level.Context);

        Assert.Equal(C4Level.Context, model.Level);
        Assert.Equal(3, model.Nodes.Count);
        Assert.Equal(2, model.Edges.Count);
        Assert.DoesNotContain(model.Nodes, n => n.Type == CatalogEntityType.Container);
        Assert.DoesNotContain(model.Edges, e => e.Type == CatalogRelationshipType.Contains);
    }

    [Fact]
    public void C4ModelBuilder_Builds_Component_Level_From_Graph()
    {
        var graph = new CatalogGraph(
            [
                new CatalogEntity { Id = "container.cli", Type = CatalogEntityType.Container, Name = "CLI" },
                new CatalogEntity { Id = "component.loader", Type = CatalogEntityType.Component, Name = "Loader", Container = "container.cli" },
                new CatalogEntity { Id = "component.generator", Type = CatalogEntityType.Component, Name = "Generator", Container = "container.cli" },
                new CatalogEntity { Id = "dataset.orders", Type = CatalogEntityType.Dataset, Name = "Orders" }
            ],
            [
                new CatalogRelationship { Id = "rel1", Type = CatalogRelationshipType.Contains, From = "container.cli", To = "component.loader" },
                new CatalogRelationship { Id = "rel2", Type = CatalogRelationshipType.Uses, From = "component.loader", To = "component.generator" },
                new CatalogRelationship { Id = "rel3", Type = CatalogRelationshipType.PublishesTo, From = "component.generator", To = "dataset.orders" }
            ],
            [],
            []);

        var model = C4ModelBuilder.Build(graph, C4Level.Component);

        Assert.Equal(3, model.Nodes.Count);
        Assert.Equal(2, model.Edges.Count);
        Assert.DoesNotContain(model.Nodes, n => n.Type == CatalogEntityType.Dataset);
        Assert.DoesNotContain(model.Edges, e => e.Type == CatalogRelationshipType.PublishesTo);
    }

    [Fact]
    public void C4Generator_GenerateContext_Produces_Context_Diagram()
    {
        var graph = new CatalogGraph(
            [
                new CatalogEntity { Id = "actor.sales", Type = CatalogEntityType.Actor, Name = "Sales Analyst" },
                new CatalogEntity { Id = "crm", Type = CatalogEntityType.System, Name = "CRM" },
                new CatalogEntity { Id = "vendor.sf", Type = CatalogEntityType.ExternalVendor, Name = "Salesforce" },
                new CatalogEntity { Id = "sales-db", Type = CatalogEntityType.Container, Name = "Sales DB" }
            ],
            [
                new CatalogRelationship { Id = "rel1", Type = CatalogRelationshipType.Uses, From = "actor.sales", To = "crm" },
                new CatalogRelationship { Id = "rel2", Type = CatalogRelationshipType.SyncsTo, From = "crm", To = "vendor.sf" },
                new CatalogRelationship { Id = "rel3", Type = CatalogRelationshipType.Contains, From = "crm", To = "sales-db" }
            ],
            [
                new CatalogViewpoint
                {
                    Id = "c4-context",
                    Name = "C4 Context",
                    Layout = "TB"
                }
            ],
            []);

        var diagram = C4Generator.GenerateContext(graph);

        Assert.Equal("c4/context.mmd", diagram.RelativePath);
        Assert.Contains("flowchart TB", diagram.Content);
        Assert.Contains("uses", diagram.Content);
        Assert.Contains("syncs_to", diagram.Content);
        Assert.DoesNotContain("contains", diagram.Content);
        Assert.DoesNotContain("Sales DB", diagram.Content);
    }

    [Fact]
    public void C4Generator_GenerateContainer_Produces_Container_Diagram()
    {
        var graph = new CatalogGraph(
            [
                new CatalogEntity { Id = "sys.crm", Type = CatalogEntityType.System, Name = "CRM" },
                new CatalogEntity { Id = "cont.api", Type = CatalogEntityType.Container, Name = "API" },
                new CatalogEntity { Id = "db.sales", Type = CatalogEntityType.Database, Name = "Sales DB" },
                new CatalogEntity { Id = "pipe.etl", Type = CatalogEntityType.Pipeline, Name = "ETL" },
                new CatalogEntity { Id = "ds.curated", Type = CatalogEntityType.Dataset, Name = "Curated" },
                new CatalogEntity { Id = "comp.loader", Type = CatalogEntityType.Component, Name = "Loader" }
            ],
            [
                new CatalogRelationship { Id = "rel1", Type = CatalogRelationshipType.DependsOn, From = "sys.crm", To = "cont.api" },
                new CatalogRelationship { Id = "rel2", Type = CatalogRelationshipType.ReadsFrom, From = "pipe.etl", To = "db.sales" },
                new CatalogRelationship { Id = "rel3", Type = CatalogRelationshipType.PublishesTo, From = "pipe.etl", To = "ds.curated" },
                new CatalogRelationship { Id = "rel4", Type = CatalogRelationshipType.Contains, From = "cont.api", To = "comp.loader" }
            ],
            [
                new CatalogViewpoint
                {
                    Id = "c4-container",
                    Name = "C4 Container",
                    Layout = "TB"
                }
            ],
            []);

        var diagram = C4Generator.GenerateContainer(graph);

        Assert.Equal("c4/container.mmd", diagram.RelativePath);
        Assert.Contains("flowchart TB", diagram.Content);
        Assert.Contains("depends_on", diagram.Content);
        Assert.Contains("reads_from", diagram.Content);
        Assert.Contains("publishes_to", diagram.Content);
        Assert.DoesNotContain("contains", diagram.Content);
        Assert.DoesNotContain("Loader", diagram.Content);
    }

    [Fact]
    public void C4Generator_GenerateComponent_Produces_Component_Diagram()
    {
        var graph = new CatalogGraph(
            [
                new CatalogEntity { Id = "cont.cli", Type = CatalogEntityType.Container, Name = "CLI Container" },
                new CatalogEntity { Id = "comp.loader", Type = CatalogEntityType.Component, Name = "Loader", Container = "cont.cli" },
                new CatalogEntity { Id = "comp.generator", Type = CatalogEntityType.Component, Name = "Generator", Container = "cont.cli" },
                new CatalogEntity { Id = "ds.curated", Type = CatalogEntityType.Dataset, Name = "Curated" }
            ],
            [
                new CatalogRelationship { Id = "rel1", Type = CatalogRelationshipType.Contains, From = "cont.cli", To = "comp.loader" },
                new CatalogRelationship { Id = "rel2", Type = CatalogRelationshipType.Uses, From = "comp.loader", To = "comp.generator" },
                new CatalogRelationship { Id = "rel3", Type = CatalogRelationshipType.PublishesTo, From = "comp.generator", To = "ds.curated" }
            ],
            [
                new CatalogViewpoint
                {
                    Id = "c4-component",
                    Name = "C4 Component",
                    Layout = "TB"
                }
            ],
            []);

        var diagram = C4Generator.GenerateComponent(graph);

        Assert.Equal("c4/component.mmd", diagram.RelativePath);
        Assert.Contains("flowchart TB", diagram.Content);
        Assert.Contains("contains", diagram.Content);
        Assert.Contains("uses", diagram.Content);
        Assert.DoesNotContain("publishes_to", diagram.Content);
        Assert.DoesNotContain("Curated", diagram.Content);
    }

    [Fact]
    public void GraphGovernance_Flags_Missing_Dataset_Classification()
    {
        var graph = new CatalogGraph(
            [
                new CatalogEntity
                {
                    Id = "dataset.orders",
                    Type = CatalogEntityType.Dataset,
                    Name = "Orders",
                    Owner = new OwnerDefinition { Team = "Data Platform" },
                    Classification = ""
                }
            ],
            [],
            [],
            []);

        var warnings = GovernanceEngine.LintGraph(graph).ToList();

        Assert.Contains(warnings, w => w.Contains("missing data classification", StringComparison.OrdinalIgnoreCase));
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
