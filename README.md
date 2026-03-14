
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

Configurable governance policy (slice 10):

Policy file:

```text
catalog/governance/policy.yaml
```

You can enable or disable governance rules and set severity (`info`, `warn`, `error`) per rule.

SQL Server import preview (slice 11):

```bash
dotnet run --project src/GitCatalog.Cli -- import-sqlserver --dry-run "Server=localhost;Database=sales;Trusted_Connection=True;TrustServerCertificate=True;" .
```

Security and operational hardening (slice 15):

Prefer non-inline secret sources:

```bash
export GITCATALOG_SQL_CONN="Server=localhost;Database=sales;User Id=...;Password=...;TrustServerCertificate=True;"
dotnet run --project src/GitCatalog.Cli -- import-sqlserver --connection-env GITCATALOG_SQL_CONN --timeout-seconds 90 .
```

or:

```bash
dotnet run --project src/GitCatalog.Cli -- import-sqlserver --connection-file ./secrets/sql-connection.txt --timeout-seconds 90 .
```

Supported import options:

- `--dry-run` preview changes without writing files
- `--connection-env <name>` read connection string from environment variable
- `--connection-file <path>` read connection string from file
- `--timeout-seconds <n>` fail import if operation exceeds timeout

The import command introspects SQL Server metadata and writes YAML table files to `catalog/tables`.

Graph-based architecture generation (slice 17):

`generate-all` now also renders Mermaid viewpoint diagrams from the canonical graph model in `docs/generated/viewpoints/*.mmd`.

Graph-based lineage generation (slice 18):

`generate-all` renders lineage diagrams from lineage viewpoints to `docs/generated/lineage/*.mmd`.

Domain dependency generation (slice 19):

`generate-all` renders cross-domain dependency maps to `docs/generated/domain/domain-dependencies.mmd`.

## Planning and Status

- Vertical slice plan and checklist: docs/AI_IMPLEMENTATION_PLAYBOOK.md
- Current implementation status: docs/IMPLEMENTATION_STATUS.md

## CI Quality Gates

GitHub Actions workflow: `.github/workflows/ci.yml`

Current CI gates run:

- restore/build/test
- catalog validate
- governance lint
- generate-all
- deterministic output check (fails if tracked files change after generation)

## Publishing Workflow

GitHub Pages workflow: `.github/workflows/publish-site.yml`

Publishing pipeline:

- restore/build
- generate-all via CLI
- package `docs/site` and `docs/generated`
- deploy via GitHub Pages actions
