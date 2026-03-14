
# AI Implementation Playbook (Drop-in Replacement)

Agents must implement GitCatalog using vertical slices.

---

# Current Status

Slices 1–9 completed (foundation).

Next work begins at Slice 10.

---

# Slice Roadmap

Slice 10 – Configurable governance policy
Slice 11 – SQL import dry-run and drift detection
Slice 12 – CI/CD quality gates
Slice 13 – Publishing workflow
Slice 14 – Performance hardening
Slice 15 – Security and operational readiness

Architecture modelling:

Slice 16 – Architecture metadata model
Slice 17 – System architecture diagram generator
Slice 18 – Data lineage generator
Slice 19 – Domain dependency diagrams

Interactive portal:

Slice 24 – Static site generation
Slice 25 – Mermaid rendering in browser
Slice 26 – Search and filtering
Slice 27 – Architecture explorer

C4 modelling:

Slice 20 – C4 metadata model
Slice 21 – Context diagram generator
Slice 22 – Container diagram generator
Slice 23 – Component diagram generator

Enterprise governance:

Slice 28 – Expanded governance rules
Slice 29 – Schema drift merge strategy
Slice 30 – Multi-source import
Slice 31 – Release governance

---

# Agent Constraints

Agents must:

- maintain layered architecture
- preserve deterministic outputs
- never overwrite curated metadata
- add tests for every slice
- update IMPLEMENTATION_STATUS.md after each slice
