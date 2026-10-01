# AGENTS.md — FallahFund

Base instructions for any agent (human or AI) working in this repository. Read this before
writing code. For the *how* of modelling a business rule, load the
`dotnet-clean-architecture` skill (`.agents/skills/dotnet-clean-architecture/`) — this file
states the rules that must hold in this repo, that skill explains the patterns.

## What this repo is

A .NET 10 / C# 14 solution using Clean Architecture (Onion), organised feature-first inside each
layer. The domain is charitable fundraising: money moves in as donations and out as
disbursements, so the interesting rules are about amounts, lifecycle states, and who is allowed to
trigger a transition. Read the actual domain types before assuming anything beyond that.

**Target framework:** `net10.0` · **Language:** C# 14 · **SDK:** 10.0.111 (also 8.0 and
9.0 installed — pin with `global.json` if the wrong SDK starts being selected)

## Repo map

The git root is this directory. Note its name ends in a trailing space
(`FallahFund /`), and so does the solution file — always quote the path.

| Project | May reference | Contains |
|---|---|---|
| `Domain` | `SharedKernel` *only* — no NuGet packages, no `Microsoft.*` | `Users/States/`, `Users/Transitions/`, `Users/ValueObjects/`, `Users/Events/`, `Users/Ports/` (interfaces only) |
| `Application` | `Domain` | `Users/Commands/`, `Users/Handlers/`, `Users/Queries/` |
| `Infrastructure` | `Domain` (+ `Application` once it exists) | `Persistence/Entities/`, `Persistence/Configuration/`, `Persistence/Migrations/`, `Mapping/`, `Repositories/` (adapters), `DependencyInjection.cs` |
| `Api` | `Application`, `Infrastructure`, `SharedKernel` | `Program.cs`, endpoints, DI wiring, OpenAPI |
| `SharedKernel` | *nothing* | `Result`, `Error`, `ErrorType`, `ResponseModel` |

Features are folders *inside* each layer project (`Domain/Users/`, later `Application/Users/`),
not projects of their own. Namespace = `<Layer>.<Feature>`, so `Domain/Users/States/UserState.cs`
declares `namespace Domain.Users;` and `Infrastructure/Repositories/EfUserRepository.cs` declares
`namespace Infrastructure.Repositories;`.

### Current state — read this before assuming a project exists

All four layers exist and the **auth slice is complete end-to-end**: register → verify email →
sign in → JWT → authenticated endpoint.

- `Application/Users/` — handlers (`RegisterUser`, `VerifyEmail`, `ResendVerification`,
  `SignInUser`), ports (`IPasswordHasher`, `IAccessTokenIssuer`, `IEmailSender`, `PasswordRules`).
- `Infrastructure/Security/` — `Pbkdf2PasswordHasher`, `JwtTokenIssuer`, `JwtOptions`.
- `Api/Endpoints/AuthEndpoints.cs` — 4 anonymous endpoints plus `GET /api/auth/me` (authorized).
- **There is no test project.** Everything above was verified with a throwaway harness that built
  the real DI graph; the checks did not survive as tests. Adding `FallahFund.Tests` is the
  highest-value next step.
- **No real email provider.** `LoggingEmailSender` writes the verification token to the log and is
  registered only in Development, guarded inside `AddDevelopmentEmailSender()`. A production
  deployment with no email provider is a startup gap, not a working system.
- **`Api/Extensions/ResultExtensions.cs` declares `namespace SharedKernel` but compiles into the
  Api assembly** and uses `StatusCodes`, so it cannot move into `SharedKernel` without giving that
  project a `<FrameworkReference Include="Microsoft.AspNetCore.App" />`. Leave it in `Api`
  (mapping errors to HTTP *is* an Api concern) or split the HTTP part out — but do not assume it
  is a `SharedKernel` type. The auth-specific HTTP mapping is `Api/Endpoints/ResultExtensions.cs`.
- **No secrets are committed.** Neither the connection string nor `Jwt:SigningKey` is in any
  `appsettings*.json`; the app **refuses to start** without both. See "Running locally".
- `.idea/` is tracked in git and is **stale** — it still references a project layout that no
  longer exists. Don't trust it; read `FallahFund .sln`.

### Running locally

```bash
# connection string is required at startup; keep it out of appsettings.json
dotnet user-secrets --project Api set "ConnectionStrings:FallahFund" \
  "Server=localhost,1433;Database=FallahFund;User Id=sa;Password=<...>;TrustServerCertificate=True"

# signing key too. Must be base64 and decode to >= 32 bytes, or startup fails.
dotnet user-secrets --project Api set "Jwt:SigningKey" "$(openssl rand -base64 48)"

dotnet ef database update --project Infrastructure    # applies Infrastructure/Persistence/Migrations
```

`Jwt:Issuer`, `Jwt:Audience` and `Jwt:AccessTokenLifetimeMinutes` are in `appsettings.json` because
they are not secrets. `Jwt:SigningKey` is not, and must never be.

Getting a verification token in Development: `LoggingEmailSender` writes it to the log at
`Warning` level, so `dotnet run --project Api` plus `POST /api/auth/register` gives you the token
in the console. This is the reason that sender is Development-only.

`dotnet ef` is run **from `Infrastructure`**, not `Api`: `Infrastructure/Persistence/FallahFundDbContextFactory.cs`
supplies a placeholder connection string so scaffolding a migration needs neither a configured one
nor a running host. Invoked from the solution root it fails with a misleading *"startup project
doesn't reference Microsoft.EntityFrameworkCore.Design"* — add `--project Infrastructure`.


## Commands

The solution filename has a trailing space — **quote it or the shell splits it wrong.**

```bash
dotnet build "FallahFund .sln"                       # verified working
dotnet run --project Api                             # http://localhost:5119, https://localhost:7026
dotnet test                                          # no test project exists yet — see below
```

There is **no test project, no `.editorconfig`, no `Directory.Build.props`, and no
`global.json`** in this repo. When you add the first one, follow
`.agents/skills/dotnet-clean-architecture/references/testing-and-checks.md`; prefer a
single `FallahFund.Tests` (xUnit v3) project that also holds the architecture test which
enforces the dependency rule, so the rule fails the build instead of failing review.

## The dependency rule

`Domain → (SharedKernel only)` · `Application → Domain` · `Infrastructure → Domain, Application`
· `Api → Application, Infrastructure`

- **Domain references no NuGet package.** Not EF Core, not `Microsoft.AspNetCore.*`, not
  MediatR, not a logging facade. If a domain file needs one of those, the design is wrong.
- **Application never references Infrastructure.** No `DbContext`, no HTTP client, no
  `IServiceCollection` extensions, no connection strings — those are ports, and the port
  interface belongs in Application (or Domain for repository interfaces).
- **Infrastructure → Application is for wiring only** (registering handlers in DI), never
  for logic. If Infrastructure needs a rule from Application, the rule belongs in Domain.
- **Nothing references Api.** Not even tests of the other layers.
- **`UserMapper` is the only file allowed to switch on lifecycle status.** The `UserStatus` enum
  exists solely for the database, and it lives in `Infrastructure/Persistence/Entities/`. A second
  status switch anywhere else means a rule has leaked out of the domain — fold it into the mapper.

## Conventions

- Files are named after the business concept, not the pattern: `Donation.cs`,
  `CampaignId.cs`, `DisbursementStatus.cs` — not `Entity1.cs`, `BaseEntity.cs`,
  `AggregateRoot.cs`. A file that isn't obviously a concept usually means two concepts
  are sharing it.
- **Parse, don't validate.** Anything arriving from HTTP, EF, or a message goes through a
  smart constructor that returns the domain type or a `Result<T, Error>`. Past that
  boundary, do not re-check what the type already guarantees.
- **Lifecycle is a type, not a field.** If something has states (`Pending` → `Confirmed` →
  `Disbursed` → `Cancelled`), model it as a sealed-record hierarchy in `Domain/Users/States`
  with transitions in `Domain/Users/Transitions` — not an enum plus nullable timestamp fields.
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
