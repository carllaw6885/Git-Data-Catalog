# Copilot Instructions --- GitCatalog

These instructions guide AI coding agents (GitHub Copilot, Copilot Chat,
etc.) when contributing to the GitCatalog repository.

## Project Purpose

GitCatalog is a **Git‑native data catalog and architecture intelligence
platform**.

It provides:

-   database documentation
-   governance validation
-   architecture diagrams
-   lineage diagrams
-   C4 architecture diagrams
-   an interactive static documentation portal

All artifacts are generated from **YAML metadata stored in Git**.

Generated outputs include:

-   Mermaid diagrams
-   Markdown documentation
-   static architecture website

Generated outputs must always be **deterministic and reproducible**.

------------------------------------------------------------------------

# Architecture Principles

Agents must follow these rules.

## Layered Architecture

The codebase uses strict layers:

CLI\
Core\
Serialization\
Validation\
Governance\
Generation\
Architecture\
Site

Rules:

-   Higher layers may depend on lower layers
-   Lower layers must never depend on higher layers
-   Domain models belong in `Core`

------------------------------------------------------------------------

# Metadata‑as‑Code Model

All catalog information is defined as YAML in the `catalog/` directory.

Examples:

catalog/databases/\
catalog/tables/\
catalog/systems/\
catalog/pipelines/\
catalog/datasets/\
catalog/domains/

Agents must:

-   treat YAML metadata as the **source of truth**
-   never overwrite curated metadata during import
-   preserve comments and human documentation where possible

------------------------------------------------------------------------

# Deterministic Generation

All generators must produce **stable deterministic outputs**.

Requirements:

-   same input → identical output
-   stable ordering of entities
-   sorted output where possible

Generated files must live in:

generated/diagrams\
generated/docs\
generated/site

These files are **derived artifacts**.

Agents must not treat them as editable sources.

------------------------------------------------------------------------

# Governance Rules

Governance rules enforce catalog quality.

Examples:

-   tables must have owners
-   tables must have descriptions
-   columns should have descriptions
-   systems must have owners
-   pipelines must define source and target

Governance severity is configurable.

Agents must implement governance rules through the **Governance
engine**, not inline checks.

------------------------------------------------------------------------

# Import Safety

Schema imports must be safe.

Required behaviors:

-   never overwrite curated metadata
-   support `--dry-run`
-   produce change previews
-   classify differences:
    -   new
    -   modified
    -   removed

------------------------------------------------------------------------

# Architecture Modelling

GitCatalog supports architecture intelligence.

Entities include:

-   System
-   Pipeline
-   Dataset
-   Domain
-   Consumer

Generators must support:

-   system architecture diagrams
-   lineage diagrams
-   domain dependency maps

------------------------------------------------------------------------

# C4 Model Generation

GitCatalog automatically generates C4 diagrams.

Supported levels:

C1 Context\
C2 Container\
C3 Component

Mapping guidance:

System → external system node\
Database → container\
Pipeline → container interaction\
Dataset → data artifact

Agents must ensure C4 diagrams remain readable and stable.

------------------------------------------------------------------------

# Interactive Documentation Site

The generated site must:

-   be static
-   require no server runtime
-   render Mermaid diagrams client‑side
-   provide search and filtering

Primary sections:

Home\
Databases\
Tables\
Systems\
Pipelines\
Datasets\
Domains\
Architecture\
Lineage\
C4

------------------------------------------------------------------------

# Testing Rules

All features must include tests.

Testing layers:

-   unit tests for models and validators
-   generator tests for diagrams
-   integration tests for catalog loading

------------------------------------------------------------------------

# Agent Development Workflow

When implementing features:

1.  follow the slice roadmap in `AI_IMPLEMENTATION_PLAYBOOK.md`
2.  implement only the current slice
3.  update `IMPLEMENTATION_STATUS.md`
4.  add tests
5.  ensure deterministic outputs

------------------------------------------------------------------------

# Things Agents Must Not Do

Do not:

-   rewrite curated YAML metadata
-   modify generated outputs manually
-   introduce non‑deterministic generation
-   collapse the layered architecture

------------------------------------------------------------------------

# Success Criteria

GitCatalog should remain:

-   Git‑native
-   deterministic
-   architecture‑aware
-   CI‑enforceable
-   easy for engineers to extend
