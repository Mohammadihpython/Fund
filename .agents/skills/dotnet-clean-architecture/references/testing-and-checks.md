# Testing and checks

Contents: [What to test where](#what-to-test-where) · [Pre-merge checklist](#pre-merge-checklist) · [Anti-patterns to flag in review](#anti-patterns-to-flag-in-review)

## What to test where

| Layer | Test style | What it's actually checking |
|---|---|---|
| Domain (transitions, mapper) | Pure unit tests, xUnit v3 or TUnit, zero mocks | Business rules the type system *can't* express (e.g. "a party over 8 needs manager approval"). Don't write a test for something a missing method overload already makes uncompilable — see `domain-modeling.md#transitions`. |
| Application (handlers) | Unit tests against an in-memory fake repository | Orchestration order and error propagation — not the database, not EF Core. |
| Infrastructure (repository adapters) | Integration tests against a real/test database (`Testcontainers` for SQL Server/Postgres, or SQLite for a lightweight in-process run) | A save-then-load round trip reconstructs an equal domain state. |
| Architecture | `NetArchTest`/`ArchUnitNET`, run in CI on every PR | The dependency rule itself — see `architecture-layers.md#the-dependency-rule-enforced`. |

## Pre-merge checklist

Copy this into a PR template if it's useful — check these before merging a change that touches domain or persistence code:

- [ ] Every type in Domain changes for one reason: a business-rule change. If a Domain file needs to change because the database schema changed, the mapper has leaked (`architecture-layers.md#the-persistence-mapping-seam`).
- [ ] `Domain.csproj` has zero `<ProjectReference>` to Infrastructure or Application.
- [ ] The architecture test suite passes and actually ran in this PR's CI (not skipped).
- [ ] No `Entity` type (the EF Core class) is referenced outside Infrastructure and its one mapper file.
- [ ] No raw `Guid`/`string`/`decimal` used for an identifier or a money value in Domain/Application, outside a smart constructor, a DTO, or an entity.
- [ ] Each handler has at most a "not found" / "wrong state" guard beyond the four orchestration steps — more than that means a rule snuck in.
- [ ] Exactly one `FromPersistence`/`ApplyToEntity` pair per aggregate — no second ad-hoc mapping function elsewhere.
- [ ] `CS8509`/`CS8524` are build errors (`SKILL.md` principle 2/3), so a new lifecycle stage can't silently skip a case.
- [ ] Any library choice that wasn't obvious has a trade-off note or at least a one-line "why not X" comment (`trade-offs.md`).

## Anti-patterns to flag in review

- A "god" entity: one class with a `Status` enum plus a dozen nullable fields, used *directly* by Domain/Application code (fine only as the EF Core entity shape — that's its job).
- A business rule validated inside a controller or handler `if`, instead of a domain transition method.
- A repository returning the raw EF Core entity to the Application layer.
- More than one place in the codebase doing `raw.Status switch { ... }`.
- `#pragma warning disable CS8509` instead of adding the missing case.
- A read model (query/DTO) built by reusing the write-side aggregate type instead of its own projection shape.
- A new dependency (MediatR, AutoMapper, a validation library) added without a one-line note on why the hand-rolled alternative wasn't enough — see `trade-offs.md`.
