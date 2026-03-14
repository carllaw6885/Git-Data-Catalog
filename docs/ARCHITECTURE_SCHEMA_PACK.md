# GitCatalog Architecture Schema Pack

This pack introduces a **canonical graph-based architecture metadata model** for GitCatalog.

The purpose is to ensure that:

- architecture diagrams
- lineage diagrams
- C4 diagrams
- governance rules
- interactive site drill-down

all work from the **same in-memory graph model**, instead of separate disconnected models.

## Why this matters

Without a canonical model, tools like GitCatalog tend to drift into multiple overlapping representations:
- schema metadata for tables
- custom objects for architecture diagrams
- separate logic for lineage
- separate mapping logic for C4

That creates duplication, inconsistency, and brittle generators.

This schema fixes that by defining:

1. **Entities**
2. **Relationships**
3. **Viewpoints**

---

## Core model

### Entities
Entities are typed nodes in the graph.

Supported entity types:
- database
- schema
- table
- column
- system
- interface
- pipeline
- dataset
- domain
- consumer
- actor
- container
- component
- external_vendor
- data_product

Common fields:
- id
- type
- name
- title
- description
- owner
- tags
- status
- criticality
- classification
- domain
- boundary
- sourceOfTruth

### Relationships
Relationships are typed edges between entities.

Supported relationship types:
- depends_on
- reads_from
- writes_to
- publishes_to
- owned_by
- belongs_to
- implements
- exposes
- ingests_from
- feeds
- uses
- syncs_to
- contains
- maps_to

Common fields:
- id
- type
- from
- to
- description
- direction
- criticality
- technology

### Viewpoints
Viewpoints are filtered renderings of the same metadata graph.

Examples:
- ER
- system architecture
- integration map
- lineage overview
- domain dependency map
- C4 context
- C4 container
- C4 component
- governance dashboard

---

## Folder structure

```text
catalog/
  entities/
    systems/
    datasets/
    domains/
    actors/
    containers/
    components/
    consumers/
    vendors/
  relationships/
    integrations/
    lineage/
    dependencies/
    ownership/
  viewpoints/
    architecture/
    c4/
    lineage/
    governance/
```

---

## Mapping guidance

### C1 Context
Render:
- actor
- system
- external_vendor
- consumer

Relationships:
- uses
- publishes_to
- syncs_to

### C2 Container
Render:
- container
- database
- pipeline
- dataset
- system/interface where appropriate

Relationships:
- reads_from
- writes_to
- depends_on
- publishes_to

### C3 Component
Render:
- component
- container
- internal services/modules

Relationships:
- uses
- implements
- contains
- depends_on

### Lineage
Render:
- system -> table
- pipeline -> table
- table -> dataset
- dataset -> consumer

Relationships:
- ingests_from
- writes_to
- feeds
- publishes_to

---

## Governance extensions enabled by this model

Examples:
- every system must have owner
- every pipeline must have at least one incoming and one outgoing relationship
- every external entity must declare boundary or vendor
- every dataset must have classification
- every critical relationship must declare technology
- every consumer-facing relationship must have purpose/description

---

## Next implementation slice

### Slice 16A — Canonical architecture schema
Goal:
Create a unified graph-based architecture metadata model that supports architecture diagrams, lineage, C4, governance, and the interactive site.

Deliverables:
- CatalogEntity base model
- CatalogRelationship base model
- entity type enum
- relationship type enum
- architecture loaders
- graph validator
- viewpoint filtering service

Acceptance criteria:
- systems, datasets, actors, domains, containers, and components load into one graph
- relationships are first-class and typed
- C4, lineage, and architecture generators read the same graph
- governance validates entities and relationships
- interactive site uses same graph for navigation and drill-down
