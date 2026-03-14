
# GitCatalog

GitCatalog is a lightweight Git‑native data catalog and architecture visualisation tool.

Key capabilities:

• Database catalog and dictionary  
• Mermaid ER diagram generation  
• Architecture diagrams (systems, pipelines)  
• Data lineage diagrams  
• C4 architecture diagrams  
• Interactive static documentation site  
• Governance rule enforcement  

Everything is stored as YAML metadata in Git and generated via a .NET CLI tool.

## CLI Commands

Run commands from the repository root:

```bash
dotnet run --project src/GitCatalog.Cli -- validate .
dotnet run --project src/GitCatalog.Cli -- lint .
dotnet run --project src/GitCatalog.Cli -- generate-all .
```

SQL Server import (slice 9):

```bash
dotnet run --project src/GitCatalog.Cli -- import-sqlserver "Server=localhost;Database=sales;Trusted_Connection=True;TrustServerCertificate=True;" .
```

The import command introspects SQL Server metadata and writes YAML table files to `catalog/tables`.
