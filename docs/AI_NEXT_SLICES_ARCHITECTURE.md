# AI Next Slices — Architecture Graph Model

Use this as the next work package for the coding agent.

## Priority sequence

### Slice 16A — Canonical architecture schema
Implement:
- CatalogEntity
- CatalogRelationship
- EntityType enum
- RelationshipType enum
- graph aggregation into the root catalog model

Add tests for:
- entity load
- relationship load
- missing reference detection
- duplicate id detection

### Slice 16B — Architecture YAML loaders
Implement loaders for:
- catalog/entities/**
- catalog/relationships/**
- catalog/viewpoints/**

Requirements:
- deterministic ordering
- support future extensibility
- tolerant parsing with clear diagnostics

### Slice 16C — Graph validator
Implement:
- missing entity references
- invalid relationship types
- invalid edge direction
- duplicate relationship ids
- domain/boundary consistency checks

### Slice 16D — Viewpoint service
Implement:
- filter entities by type
- filter by domain
- filter by tags
- filter by boundary
- select edges relevant to viewpoint

### Slice 17 revision — System architecture generator on graph
Refactor architecture generation to consume the graph model only.

### Slice 18 revision — Lineage generator on graph
Refactor lineage generation to consume the graph model only.

### Slice 20 revision — C4 mapping from graph
Generate C1/C2/C3 views by filtering graph entities and relationships.

## Constraints
- do not create a second disconnected architecture model
- do not bypass graph validation in generators
- treat current table/database metadata as compatible inputs that can be normalized into graph entities
- update IMPLEMENTATION_STATUS.md after each slice
