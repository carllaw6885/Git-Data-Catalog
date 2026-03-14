
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

The CLI supports six commands:

```bash
dotnet run --project src/GitCatalog.Cli -- validate [repoRoot]
dotnet run --project src/GitCatalog.Cli -- lint [repoRoot]
dotnet run --project src/GitCatalog.Cli -- generate-all [repoRoot]
dotnet run --project src/GitCatalog.Cli -- release-check [repoRoot] [--fail-on-warn|--fail-on-error]
dotnet run --project src/GitCatalog.Cli -- import [--source <sqlserver|postgres>] [import options] [repoRoot]
dotnet run --project src/GitCatalog.Cli -- import-sqlserver [import options] [repoRoot]
```

### Command Reference

1. `validate`
- Loads and validates table catalog metadata plus architecture graph metadata.
- Fails (non-zero exit) on loader diagnostics or validation errors.

2. `lint`
- Applies governance policies for table metadata and graph metadata.
- Reads policy from:

```text
catalog/governance/policy.yaml
```

- Emits findings (with policy severity), but does not block on warnings alone.

3. `generate-all`
- Runs validation and governance before generation.
- Writes deterministic artifacts (write-if-changed behavior) to:
- `docs/generated/er.mmd`
- `docs/generated/index.md` and table docs under `docs/generated/tables/`
- `docs/generated/viewpoints/*.mmd`
- `docs/generated/lineage/*.mmd`
- `docs/generated/domain/domain-dependencies.mmd`
- `docs/generated/c4/context.mmd`
- `docs/generated/c4/container.mmd`
- `docs/generated/c4/component.mmd`
- `docs/site/*` static portal assets

4. `release-check`
- Runs validate + governance and evaluates release readiness.
- Default mode is strict (`--fail-on-warn` behavior).
- Use `--fail-on-error` to allow warnings and fail only on errors.
- Exits non-zero when release gate rules are not satisfied.

5. `import`
- Multi-source import entrypoint.
- `--source sqlserver` is implemented.
- `--source postgres` is recognized but currently returns a not-implemented message.

6. `import-sqlserver`
- SQL Server-specific import command (backwards-compatible alias behavior).
- Reads schema, merges safely with curated YAML, emits change classifications and drift details.

### Import Options

Supported options for `import` and `import-sqlserver`:

- `--dry-run` preview changes without writing files
- `--connection-env <name>` read connection string from environment variable
- `--connection-file <path>` read connection string from file
- `--timeout-seconds <n>` fail import if operation exceeds timeout (default: 120)
- `--source <sqlserver|postgres>` (`import` command only; default: `sqlserver`)

Connection source rules:

- Use only one of inline connection string, `--connection-env`, or `--connection-file`
- Inline connection strings are supported but produce a security warning
- Error messages redact common credential keys (for example `Password`, `Pwd`, `User ID`, `Uid`)

### Import Behavior

Import outputs include:

- change kind per table: `Create`, `Update`, `Unchanged`, `Remove`
- drift details for schema differences
- merge-strategy details showing where curated descriptions, owners, and FK metadata were retained

Generated/updated YAML files are written under:

```text
catalog/tables/
```

### Practical Examples

Run the core pipeline:

```bash
dotnet run --project src/GitCatalog.Cli -- validate .
dotnet run --project src/GitCatalog.Cli -- lint .
dotnet run --project src/GitCatalog.Cli -- generate-all .
dotnet run --project src/GitCatalog.Cli -- release-check . --fail-on-error
```

Secure SQL Server import via environment variable:

```bash
export GITCATALOG_SQL_CONN="Server=localhost;Database=sales;User Id=...;Password=...;TrustServerCertificate=True;"
dotnet run --project src/GitCatalog.Cli -- import --source sqlserver --connection-env GITCATALOG_SQL_CONN --timeout-seconds 90 .
```

Import dry run using connection file:

```bash
dotnet run --project src/GitCatalog.Cli -- import-sqlserver --dry-run --connection-file ./secrets/sql-connection.txt --timeout-seconds 90 .
```

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

## Install In Other Repositories

After publishing a tool package (for example via `.github/workflows/publish-tool.yml`), developers can install the CLI in any repo as a local .NET tool.

1. Add the GitHub Packages source (one-time per machine):

```bash
dotnet nuget add source "https://nuget.pkg.github.com/<ORG>/index.json" \
	--name github-<ORG> \
	--username <GITHUB_USERNAME> \
	--password <GITHUB_PAT_WITH_read_packages> \
	--store-password-in-clear-text
```

2. In the target repository, create a tool manifest:

```bash
dotnet new tool-manifest
```

3. Install a pinned version of the CLI tool:

```bash
dotnet tool install --local GitCatalog.Cli \
	--version 1.0.0 \
	--add-source "https://nuget.pkg.github.com/<ORG>/index.json"
```

4. Run commands from that repo:

```bash
dotnet tool run gitcatalog validate .
dotnet tool run gitcatalog lint .
dotnet tool run gitcatalog generate-all .
dotnet tool run gitcatalog release-check . --fail-on-error
```

5. Upgrade later:

```bash
dotnet tool update --local GitCatalog.Cli --version 1.1.0
```
