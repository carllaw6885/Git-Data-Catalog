# PRD Appendix — Canonical Architecture Metadata Schema

This appendix extends the GitCatalog PRD.

## Addition to product scope

GitCatalog shall maintain a **canonical graph-based architecture model** that unifies:
- data catalog metadata
- architecture modelling
- lineage
- C4 modelling
- interactive site navigation
- governance validation

## New capability area: Architecture Graph

The platform shall load all architecture-relevant metadata into a common graph representation.

### Graph node types
- system
- dataset
- domain
- actor
- container
- component
- consumer
- vendor
- database
- table

### Graph edge types
- depends_on
- uses
- ingests_from
- writes_to
- publishes_to
- contains
- belongs_to
- implements
- syncs_to
- owned_by

## Product requirement

All future diagram generators must operate on the common graph model.
No generator should require a separate parallel metadata structure.

## Benefits
- consistent modelling
- simplified generator design
- better governance
- stronger interactive navigation
- easier extension for future diagrams
