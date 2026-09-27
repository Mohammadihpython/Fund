# Trade-offs

Contents: [Why bother writing these down](#why-bother-writing-these-down) · [Template](#template) · [Worked example: MediatR](#worked-example-mediatr) · [Shorter worked examples](#shorter-worked-examples)

## Why bother writing these down

Most architectural regret isn't from picking the wrong option — it's from picking a reasonable option for reasons nobody wrote down, so six months later nobody can tell whether the reasons still hold. A trade-off note is cheap when the decision is fresh (you already did the thinking) and expensive to reconstruct later (someone has to re-derive context from git blame). Write one whenever you chose between two options that a reasonable engineer could disagree with — not for decisions with only one sane answer.

Keep it to what fits on one screen. This is a decision record, not a design document — if it needs subheadings and a table of contents, it has grown into something else.

## Template

```markdown
# [Short, decision-framed title, e.g. "Command dispatch: hand-rolled vs. MediatR"]

**Status:** Proposed / Accepted / Superseded by [link]
**Date:** YYYY-MM-DD

## Context
What forced this decision? (One or two sentences — the constraint, not the whole backstory.)

## Options considered
- **Option A** — what it is, in one line.
- **Option B** — what it is, in one line.
(Rarely more than 3. If there are 5 real options, the decision is being made too early.)

## Decision
Which option, and the one or two reasons that actually tipped it — not every pro and con, just the ones that mattered.

## Trade-offs accepted
What does *not* picking the other option cost us? Be concrete: a feature we don't get, a risk we're taking on, a migration we're deferring.

## Revisit when
The condition that would make this worth reopening (a number crossed, a library's status changes, a team constraint lifts). If there's no such condition, say "no planned revisit" rather than leaving it implicit.
```

Store these as `docs/decisions/NNNN-title.md` in the repo, numbered sequentially, never edited after `Accepted` — a changed decision gets a new file that supersedes the old one. That history is the point.

## Worked example: MediatR

```markdown
# Command dispatch: hand-rolled dispatcher vs. MediatR

**Status:** Accepted
**Date:** 2026-01-14

## Context
Starting a new .NET 10 vertical-slice project. Need something to route a command
object to its handler from the API layer. MediatR is what most Clean Architecture
tutorials still default to, but it moved to a dual (free/commercial) license in
mid-2025 — free tier covers individuals and companies under roughly $5M revenue,
paid tiers above that.

## Options considered
- **MediatR** — mature, widely known, pipeline behaviors (logging/validation/
  transactions) out of the box. Licensing cost applies once the company crosses
  the free-tier threshold.
- **Hand-rolled dispatcher** — one interface (`ICommandHandler<TCommand, TResult>`),
  one class doing a DI lookup, ~20 lines total. No pipeline behaviors included.
- **Source-generator alternative** (e.g. a MediatR-API-compatible library, or
  Wolverine) — no licensing question, but a new dependency to vet, and less
  battle-tested than MediatR for edge cases like streaming requests.

## Decision
Hand-rolled dispatcher. The project has ~15 commands total — nowhere near
where MediatR's pipeline-behavior machinery earns its weight, and this removes
a licensing question from a project that doesn't need to be having it. Revisit
with a real library if the handler count or cross-cutting-concern complexity
grows enough that a hand-rolled registry starts feeling like reinvented plumbing.

## Trade-offs accepted
No built-in pipeline behaviors — logging/validation around each handler is
either duplicated per-handler or added by hand to the dispatcher. No
notification/fan-out (one command, one handler) — if a "many handlers react to
one event" need shows up, that's a separate decision, not a reason to reach for
MediatR now.

## Revisit when
Handler count exceeds ~40, or more than two cross-cutting concerns (logging,
validation, retry) need to wrap every handler — at that point a real pipeline
library's cost is worth re-evaluating against continuing to hand-roll it.
```

Use this one as the shape to imitate — note that "Decision" doesn't re-list every pro/con from "Options considered", it names the one or two things that actually tipped it. That's what makes it readable in thirty seconds a year from now.

## Shorter worked examples

For decisions that don't need the full template, a two-line note in the PR description or a code comment is enough — but still name the option *not* taken, so a future reader knows it was considered rather than missed.

- **EF Core JSON mapping — complex type vs. owned entity type.** EF Core 10 recommends `ComplexProperty(...).ToJson()` over the older owned-entity-type JSON pattern for new code; owned entities have sharper edges around change tracking and querying that complex types avoid. Only reach for owned entities if you specifically need their relational (non-JSON) table-splitting behavior.
- **Testing framework — xUnit v3 vs. TUnit.** xUnit v3 is the safe, VSTest-compatible default. TUnit (Microsoft.Testing.Platform-only, source-generator discovery, Native AOT-friendly) is worth it specifically when test-suite startup time or AOT publishing is a real constraint — not as a default swap for its own sake, and not mixed into the same `dotnet test` run as a VSTest-based framework.
- **API surface — Minimal APIs vs. Controllers.** Minimal APIs for small, focused endpoint counts and when the compositional style (each endpoint as a small delegate/class) matches the vertical-slice organization already in use. Controllers still earn their place with large endpoint counts that benefit from shared filters/conventions, or a team more comfortable with the attribute-routing style — this is a team-fit trade-off as much as a technical one.
- **Architecture enforcement — NetArchTest vs. a hand-written Roslyn check.** NetArchTest for the common case (dependency-direction rules); a small hand-written analyzer only once a rule is too specific for its fluent API to express comfortably — don't reach for a custom Roslyn analyzer as a first move, the maintenance cost is real.
- **Result type — hand-rolled `Result<T, Error>` vs. a library (`OneOf`, `LanguageExt`).** Hand-rolled is one small file and keeps the domain project dependency-free (principle 6 in `SKILL.md`); a library pays off once you need richer combinators (`Match`, `Bind` chains across many types) across a large codebase. Don't add the dependency until the hand-rolled version is visibly straining.
