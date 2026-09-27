---
name: dotnet-clean-architecture
description: Design, write, and review C#/.NET backend code — domain models, business rules, and the trade-offs behind them — using Clean/Onion Architecture in Vertical Slices plus DDD tactics (smart constructors for "parse, don't validate", sealed-record state machines instead of enum-plus-nullable-fields, ports/adapters, event-storming-derived aggregates). Targets .NET 10 LTS / C# 14 and current library trade-offs (EF Core 10, xUnit v3 vs TUnit, MediatR's 2025 commercial licensing). Use whenever building/reviewing a .NET/C# backend, modeling a domain (orders, reservations, carts, subscriptions — anything with a status), writing/reviewing a business rule or invariant, structuring a solution into layers, choosing between competing .NET libraries, or writing a trade-off note. Trigger even without "Clean Architecture" or "DDD" by name — e.g. "write the cancellation rule for a booking", "should this be an enum or something else", "should I use MediatR here", "review this handler".
---

# .NET Clean Architecture, Business Rules & Trade-offs

## What this skill is for

Writing business logic is easy to get wrong in two opposite ways: scattering `if` checks everywhere until nobody can say what the actual rules are (anemic, "primitive-soup" code), or building deep class hierarchies and abstractions that don't map to anything a domain expert would recognize (astronaut architecture). This skill is a compact, opinionated middle path — patterns that make illegal states genuinely impossible to write, code that reads like the business rule it implements, and layering that survives infrastructure changes without a rewrite.

It targets **.NET 10 (LTS, supported to Nov 2028)** and **C# 14**. Treat every pattern here as a default worth explaining, not a law — if the user's constraints point elsewhere, say so and explain the trade-off (see `references/trade-offs.md`).

## Orientation — where to go next

This file has the load-bearing principles and the workflow for writing a rule end-to-end. For depth, open the relevant reference file rather than re-deriving it:

| You need to... | Open |
|---|---|
| Model an entity, a value object, or a "thing with a status" (order, reservation, subscription...) | `references/domain-modeling.md` |
| Decide which project/layer a piece of code belongs in, wire up the dependency rule, write a handler | `references/architecture-layers.md` |
| Use current C# 14 / .NET 10 syntax correctly (field keyword, extension members, null-conditional assignment, primary constructors, required members) | `references/dotnet10-idioms.md` |
| Choose between two reasonable libraries/patterns, or write the decision down | `references/trade-offs.md` |
| Decide what to test where, or build an architecture-enforcing test | `references/testing-and-checks.md` |
| Start a new value object / state machine / trade-off note from a skeleton instead of a blank file | `assets/templates/` |

## The principle chain

Seven ideas, in the order they should influence a design. Each one exists to catch a specific class of bug as early as possible — ideally at compile time, because a compile error is the cheapest bug there is: nobody has to notice it, reproduce it, or ship a hotfix for it.

1. **Parse, don't validate.** Data crossing a boundary (an HTTP body, a database row, a message payload) is untrusted until a smart constructor turns it into a domain object that is *guaranteed* valid. After that point, never re-check what the type already promises.
2. **Illegal states are unrepresentable.** If a cancelled reservation should never have a `SeatedAt` timestamp, don't give it a nullable `SeatedAt` — give it no such property, by making "cancelled" a distinct type.
3. **Status is a type, not a field.** `Status == "Cancelled"` is a database concern. In the domain it's a distinct type, so the compiler — not a scattered `if` — decides which operations are even possible to call.
4. **Services orchestrate; they don't decide.** An application handler reads like a table of contents: validate → fetch → call the domain → persist → return. Any `if` that encodes a business rule belongs one layer in, on the domain type itself.
5. **Primitives are boundary-only.** Raw `Guid`, `string`, `decimal` are fine at the very edge. One line further in, they become `ReservationId`, `Money`, `PartySize` — types that can't be swapped for each other by accident.
6. **The dependency rule is enforced, not just followed.** `Domain → (nothing)`, `Application → Domain`, `Infrastructure → Domain, Application`. If the Domain project needs a reference to Infrastructure to compile, the design has already leaked — catch it at build time, not in review.
7. **Every non-obvious choice gets one paragraph of "why".** Not every decision needs a formal ADR, but if you picked one of two reasonable options — an enum vs. a sealed hierarchy, MediatR vs. a hand-rolled dispatcher — write down what you'd tell a teammate who asks "why not the other way?" six months from now. See `references/trade-offs.md`.

## Workflow: turning a business rule into code

When the user describes a rule ("a reservation can only be cancelled while it's pending", "a discount can't take the total below zero", "an order can't ship without a confirmed payment"), work through this, not necessarily narrating every step out loud:

1. **Name it as a past-tense event first.** "Reservation cancelled", not "cancel logic". If you can't name the event, you don't understand the rule yet — go ask, don't guess. This is the event-storming instinct: a business rule almost always corresponds to one event with a command upstream and a state change downstream.
2. **Does this rule introduce or depend on a lifecycle stage?** If the entity now has a state the type system doesn't represent yet ("pending" vs "confirmed" vs "cancelled"), model it as a sealed-record hierarchy before writing the rule itself — see `references/domain-modeling.md#status-as-type`. Don't bolt another nullable field or bool flag onto an existing type; that's the exact anti-pattern principle 2 exists to prevent.
3. **Is this rule really a validation at a boundary?** ("A party size must be between 1 and 20", "an email must be well-formed") — that's a smart constructor, not a domain transition. See `references/domain-modeling.md#smart-constructors`.
4. **Write the rule as a static method on the domain type**, taking the *specific* predecessor type as its parameter (not the abstract base). Return the new state, or a `Result<T, Error>` if the rule can fail. This is what makes illegal calls a compile error instead of a runtime surprise — see `references/domain-modeling.md#transitions`.
5. **Wire it through a thin handler**: validate the command → fetch via the repository interface → call the domain method → persist → return. If the handler has more than one `if` beyond a not-found/wrong-state guard, a rule has leaked into the wrong layer — move it back to step 4.
6. **If you chose between two workable designs, write the one-paragraph trade-off** (`references/trade-offs.md#template`). This is cheap when the decision is fresh and expensive to reconstruct later.

Apply the same shape whether the "thing with a rule" is a shopping cart, a restaurant reservation, a subscription, or an approval workflow — the nouns change, the pattern doesn't.

## .NET 10 / C# 14 — what's different from older guidance

If you (or the user) learned Clean Architecture on .NET 6/7/8, three things are worth updating. Full snippets are in `references/dotnet10-idioms.md`; the headline points:

- **C# 14 adds real syntax for patterns that used to need boilerplate**: the `field` keyword for validation-in-an-accessor without a hand-written backing field, `extension` blocks for adding properties/operators/static members to a type you don't own, and null-conditional assignment (`customer?.Order = x`). Use these — they remove ceremony from exactly the value-object and mapper code this skill produces a lot of.
- **MediatR and AutoMapper went dual-licensed in mid-2025** (free for individuals and companies under $5M revenue; paid tiers above that — verify current terms before assuming, pricing details drift). For a fresh .NET 10 project, don't reach for MediatR by default the way older tutorials do. A hand-rolled dispatcher is now genuinely easy to write with C# 14 (source-gen or a `FrozenDictionary<Type, ...>` registry), and it removes a licensing question nobody wants to answer mid-project. See `references/trade-offs.md#mediatr`.
- **EF Core 10 changes two defaults worth knowing**: named query filters (multiple independent global filters per entity, e.g. soft-delete *and* multi-tenancy, toggled individually instead of all-or-nothing), and complex types mapped to JSON columns are now the recommended replacement for owned-entity-type JSON mapping. Don't reach for the old owned-entity JSON pattern in new .NET 10 code.

## Writing style for the code itself

- Prefer explaining *why* a rule exists in a one-line comment over a wall of defensive `if`s — the reader should be able to tell the business reason a check exists, not just that it exists.
- Keep entity/value-object files small and named after the business concept, not the pattern ("Reservation.cs", not "AggregateRoot.cs").
- A domain method's signature should make invalid calls impossible to *write*, not just impossible to *pass*. If you find yourself adding a runtime guard clause for something the type system could rule out instead, prefer the type-system fix.
- Don't perform the full ceremony (sealed-record hierarchy, repository port/adapter, CQRS-shaped handler) for something with no real lifecycle or no real infrastructure dependency — that's cargo-culting the pattern, not applying it. A calculator function is just a calculator function.
