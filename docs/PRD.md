
# GitCatalog PRD (Drop-in Replacement)

## Vision
GitCatalog is a Git-native data catalog and architecture intelligence platform.
It provides schema documentation, governance enforcement, architecture visualisation,
lineage mapping, C4 modelling, and a static interactive documentation portal.

Everything is stored as **metadata-as-code** and rendered into diagrams,
documentation, and architecture views.

---

# Core Principles

- Git-native metadata
- Deterministic generation
- CI enforceable governance
- Extensible architecture model
- Static publishable outputs
- Developer-first CLI

---

# MVP Scope

MVP includes:

- YAML catalog metadata
- Catalog validation
- Governance linting
- Mermaid ER diagrams
- Markdown documentation
- SQL schema import
- CI integration

Governance is an MVP capability.

---

# Architecture Intelligence Scope

GitCatalog extends the catalog into architecture modelling.

Additional entities:

- Systems
- Pipelines
- Datasets
- Domains
- Consumers

These entities allow generation of:

- system architecture diagrams
- data lineage diagrams
- domain dependency maps

---

# C4 Modelling

GitCatalog generates C4 architecture views.

Supported levels:

C1 – Context  
C2 – Container  
C3 – Component  

Mapping rules:

System -> External system node  
Database -> Container  
Pipeline -> Container interaction  
Dataset -> Container data artifact  

---

# Interactive Site

Generated site includes:

- searchable catalog
- entity pages
- Mermaid diagrams rendered client-side
- lineage explorer
- architecture explorer
- governance dashboard

Static output is deployable to GitHub Pages or Azure Static Web Apps.

---

# Enterprise Roadmap

Phase A – Production readiness
Phase B – Architecture intelligence
Phase C – C4 modelling
Phase D – Interactive exploration
Phase E – Enterprise governance
