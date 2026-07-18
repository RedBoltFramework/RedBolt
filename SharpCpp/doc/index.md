# SharpCpp v2 Design Documents

## Overview

V2 redesigns the SharpCpp generator around a single principle: **the marshaller is the unit of configuration**. Every type that crosses the C++/C# boundary is configured by its `Marshaller<T>` specialization. The generator core is small and uniform; type-specific behavior is declared through C++ attributes, not encoded in renderer branching.

## Documents

| # | Title | Description |
|---|---|---|
| [[00-rationale]] | Rationale for v2 | Why v2, what's wrong with v1, lessons learned |
| [[01-architecture]] | Architecture | Top-down architecture overview, pipeline, uniform model |
| [[02-marshaller-system]] | Marshaller System | The central `Marshaller<>` + `LibraryImport` design |
| [[03-two-phase-parsing]] | Two-Phase Parsing | Parse API then instantiate marshaller configuration |
| [[04-type-handling]] | Type Handling | Index → 17 detailed files covering every binding feature with all variants |
| [[05-configuration-attributes]] | Configuration Attributes | The C++ attribute vocabulary |
| [[06-core-minimal]] | Core Minimal | What stays in core vs what's configuration |
| [[07-marshaller-contract]] | Marshaller Contract | Exact extractable surface the generator may read from `Marshaller<T>` |
| [[08-metadata-contract]] | Metadata Contract | Export/import schema, versioning, merge precedence, invalidation |
| [[09-pass-boundaries]] | Pass Boundaries | Stage ownership, pass responsibilities, renderer assumptions |
| [[10-ownership-lifetime]] | Ownership & Lifetime | Unified contract for handles, strings, callbacks, caches, keep-alives |
| [[11-project-shape-blueprint]] | Project Shape Blueprint | Proposed repo/module layout for the v2 implementation |
| [[12-generated-output-and-module-api]] | Generated Output & Module API | Output file contract plus cross-module export/import rules |
| [[13-rewrite-strategy]] | Rewrite Strategy | How to build v2 as an independent project while selectively reusing v1 pieces |
| [[14-diagnostics-and-failure-model]] | Diagnostics & Failure Model | Fatal vs degradable failures, stages, codes, reporting rules |
| [[15-compatibility-policy]] | Compatibility Policy | Generator, metadata schema, support runtime, and import compatibility rules |

## Relationship to v1 docs

The `doc/` folder remains authoritative for shipped v1 code. The `doc-v2/` folder is authoritative for the target v2 design only. Contradictions mean planned redesign, not current implementation behavior.
