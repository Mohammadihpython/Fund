# AGENTS.md — FallahFund

Base instructions for any agent (human or AI) working in this repository. Read this before
writing code. For the *how* of modelling a business rule, load the
`dotnet-clean-architecture` skill (`.agents/skills/dotnet-clean-architecture/`) — this file
states the rules that must hold in this repo, that skill explains the patterns.

## What this repo is

A .NET 10 / C# 14 solution using Clean Architecture (Onion) with vertical slices. The domain
is charitable fundraising: money moves in as donations and out as disbursements, so the
interesting rules are about amounts, lifecycle states, and who is allowed to trigger a
transition. Read the actual domain types before assuming anything beyond that.

**Target framework:** `net10.0` · **Language:** C# 14 · **SDK:** 10.0.111 (also 8.0 and
9.0 installed — pin with `global.json` if the wrong SDK starts being selected)

## Repo map

The git root is this directory. Note its name ends in a trailing space
(`FallahFund /`), and so does the solution file — always quote the path.

| Project | May reference | Contains (folder skeleton on disk) |
|---|---|---|
| `FallahFund.Domain` | *nothing* — no NuGet packages, no `Microsoft.*` | `Entities/`, `ValueObjects/`, `States/`, `Transitions/`, `Events/`, `Repositories/` (interfaces only), `Shared/` |
| `FallahFund.Application` | `Domain` | `Commands/`, `Handlers/`, `Shared/` |
| `FallahFund.Infrastructure` | `Application`, `Domain` | `Entities/` (persistence models), `Mapping/`, `Repositories/` (adapters), `Shared/` |
| `FallahFund.Api` | `Application`, `Infrastructure` | `Program.cs`, endpoints, DI wiring, OpenAPI |

### Current state — read this before assuming a project exists

Only **`FallahFund.Api`** is committed (the stock `dotnet new web` template plus the
weatherforecast sample endpoint). The other three project folders exist on disk as
**empty directories with stale `bin/` and `obj/` output from a build that was deleted** —
their `.csproj` files are gone and they are **not in the solution file**.

So:

- `FallahFund.Api/bin/Debug/net10.0/` contains `FallahFund.Domain.dll`,
  `FallahFund.Application.dll`, `FallahFund.Infrastructure.dll`. **These are stale
  artifacts of code that no longer exists.** Do not read them as the current design, do
  not decompile them, do not trust them as a reference for what to build.
- The last build's dependency graph, recovered from `FallahFund.Api.deps.json`, is the
  intended one and matches the table above: Domain ← Application ← Infrastructure ← Api,
  with `Microsoft.EntityFrameworkCore(.SqlServer)` 10.0.0 in Infrastructure and
  `Microsoft.AspNetCore.OpenApi` 10.0.11 in Api.
- If you need the other three projects, **create them** (see below). Do not assume a
  project reference resolves just because a folder with that name exists.

To recreate the missing projects:

```bash
dotnet new classlib -n FallahFund.Domain        -o FallahFund.Domain        -f net10.0
dotnet new classlib -n FallahFund.Application   -o FallahFund.Application   -f net10.0
dotnet new classlib -n FallahFund.Infrastructure -o FallahFund.Infrastructure -f net10.0
dotnet sln "FallahFund .sln" add FallahFund.Domain FallahFund.Application FallahFund.Infrastructure
dotnet add FallahFund.Application   reference FallahFund.Domain
dotnet add FallahFund.Infrastructure reference FallahFund.Application FallahFund.Domain
dotnet add FallahFund.Api           reference FallahFund.Application FallahFund.Infrastructure
```

Delete `Class1.cs` and the default `bin/`/`obj/` in each new project before adding code.
Re-enable the properties the template already sets (`Nullable`, `ImplicitUsings`).

## Commands

The solution filename has a trailing space — **quote it or the shell splits it wrong.**

```bash
dotnet build "FallahFund .sln"                       # verified working
dotnet run --project FallahFund.Api                  # http://localhost:5119, https://localhost:7026
dotnet test                                        # no test project exists yet — see below
```

There is **no test project, no `.editorconfig`, no `Directory.Build.props`, and no
`global.json`** in this repo. When you add the first one, follow
`.agents/skills/dotnet-clean-architecture/references/testing-and-checks.md`; prefer a
single `FallahFund.Tests` (xUnit v3) project that also holds the architecture test which
enforces the dependency rule, so the rule fails the build instead of failing review.

## The dependency rule

`Domain → (nothing)` · `Application → Domain` · `Infrastructure → Application, Domain` ·
`Api → Application, Infrastructure`

- **Domain references no NuGet package.** Not EF Core, not `Microsoft.AspNetCore.*`, not
  MediatR, not a logging facade. If a domain file needs one of those, the design is wrong.
- **Application never references Infrastructure.** No `DbContext`, no HTTP client, no
  `IServiceCollection` extensions, no connection strings — those are ports, and the port
  interface belongs in Application (or Domain for repository interfaces).
- **Infrastructure → Application is for wiring only** (registering handlers in DI), never
  for logic. If Infrastructure needs a rule from Application, the rule belongs in Domain.
- **Nothing references Api.** Not even tests of the other layers.

## Conventions

- Files are named after the business concept, not the pattern: `Donation.cs`,
  `CampaignId.cs`, `DisbursementStatus.cs` — not `Entity1.cs`, `BaseEntity.cs`,
  `AggregateRoot.cs`. A file that isn't obviously a concept usually means two concepts
  are sharing it.
- **Parse, don't validate.** Anything arriving from HTTP, EF, or a message goes through a
  smart constructor that returns the domain type or a `Result<T, Error>`. Past that
  boundary, do not re-check what the type already guarantees.
- **Lifecycle is a type, not a field.** If something has states (`Pending` → `Confirmed` →
  `Disbursed` → `Cancelled`), model it as a sealed-record hierarchy in `Domain/States`
  with transitions in `Domain/Transitions` — not an enum plus nullable timestamp fields.
  Adding a `bool IsCancelled` to an existing type is the anti-pattern this rule exists to
  prevent.
- **Primitives are boundary-only.** `Guid`/`string`/`decimal` are fine in an API request
  DTO and in an EF mapping. One layer in, they are `CampaignId`, `DonorId`, `Money`,
  `Amount`. Do not pass a raw `Guid` between Application and Domain.
- **Amounts are `decimal`, never `double`/`float`.** This is money; rounding errors are
  defects. Currency handling and rounding mode are domain decisions — make them explicit
  in the value object, don't leave them to the database column type.
- **A handler orchestrates; it does not decide.** Validate → resolve via port → call the
  domain → persist → return. More than one `if` in a handler beyond a not-found guard
  means a business rule leaked into the wrong layer; move it to the domain type.
- **EF Core 10 specifics:** use named query filters for soft-delete/multi-tenancy, and map
  complex types to JSON columns rather than owned-entity JSON mapping. Keep configurations
  in `Infrastructure/Mapping/`, one file per aggregate, never in `Program.cs`.
- **No MediatR by default.** It went dual-licensed in mid-2025; verify current terms
  before adding it, and prefer a hand-rolled dispatcher (a `FrozenDictionary<Type, …>`
  registry is ~30 lines). See `references/trade-offs.md` in the skill.
- **One-paragraph "why" for non-obvious choices.** If you picked one of two reasonable
  designs, write down what you'd tell someone asking "why not the other way?" in six
  months. Not everything needs a formal ADR.

## Repo hygiene

- **Never commit secrets.** No connection strings, API keys, or JWT signing keys in
  `appsettings*.json` — use user-secrets for local development and environment
  configuration for deployed environments. `appsettings.json` is currently committed and
  correctly contains logging config only; keep it that way.
- `.idea/` is tracked in git (including the `FallahFund /` module folder). Don't add more
  IDE files, and don't reorganize what's there unless asked.
- `bin/` and `obj/` are gitignored. Stale output on disk is not a signal that a project
  exists — check for the `.csproj` and the solution entry instead.
- Commit only when explicitly asked. When you do, stage only the intended files and match
  the existing message style (the history is just `init`, so there is no house style yet —
  use short imperative subjects).

## When you're about to start a feature

1. Name the business event in past tense ("Donation disbursed"). Can't name it? You don't
   understand the rule yet — ask, don't guess.
2. If it introduces a lifecycle stage, model the state hierarchy first, then the rule.
3. If it's really a boundary check ("amount must be > 0"), it's a smart constructor, not a
   transition.
4. Write the rule as a static method on the domain type, taking the *specific* predecessor
   state as its parameter.
5. Wire a thin handler through it.
6. Note the trade-off if there was a real alternative.

Don't perform the full ceremony for something with no lifecycle and no infrastructure
dependency. A calculator function is just a calculator function.
