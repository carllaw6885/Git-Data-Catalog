# Copilot Architecture Instructions — GitCatalog

These instructions extend the repo guidance for architecture modelling.

## Canonical graph rule
All future architecture, lineage, C4, governance and site navigation work must use a **single graph-based metadata model**.

Do not create:
- separate lineage-only models
- separate C4-only models
- separate site navigation models

Instead:
- load metadata into `CatalogEntity` nodes
- load metadata into `CatalogRelationship` edges
- render viewpoints by filtering the graph

## Required design rules
- relationships must be first-class and typed
- generators must not directly parse YAML
- generators must consume validated in-memory graph objects
- site drill-down must use graph adjacency/navigation
- governance rules should validate both nodes and edges

## Compatibility rule
Existing table/database metadata should remain supported, but normalized into graph entities internally.

## Implementation sequence
1. graph entities
2. graph relationships
3. graph validation
4. viewpoint service
5. graph-based architecture generator
6. graph-based lineage generator
7. graph-based C4 generators
