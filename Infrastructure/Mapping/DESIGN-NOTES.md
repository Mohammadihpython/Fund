# Infrastructure: one flat row, one status switch, one mapper

**Status:** Accepted
**Date:** 2026-10-01

## Context

The user lifecycle lives in the domain as a sealed-record hierarchy
(`PendingVerification | Active | LockedOut | Deactivated`). That shape has to survive a
relational store, and a relational row genuinely is "one shape, some columns unused right now" —
`WHERE Status = 'LockedOut'` is a query the business will want and the type system cannot
express. So the domain shape and the row shape have to coexist, which is exactly the situation
where a rule quietly leaks inward.

## Options considered

- **Option A** — map the sealed records directly with EF value converters, and let EF own the
  polymorphic read (`OfType<LockedOut>()`) via TPH, TPT, or TPC inheritance mapping.
- **Option B** — a flat `UserEntity` with one `Status` column and a nullable column per
  stage-specific value, plus a single `UserMapper` that translates in both directions.

## Decision

Option B. The deciding factor is where the *knowledge* lives. With Option A the state→column
mapping is spread across EF's own model-building code and across whoever calls
`OfType<LockedOut>()`; with Option B there is exactly one file that knows a row's `Status` column
exists, and it is 200 lines of mechanical field copying. Everything else in `Infrastructure` —
the repository, the DI extension, `Program.cs` — is written without naming a single lifecycle
stage. That is the concrete test of whether the port is doing its job: if swapping EF for
Dapper/Postgres requires edits outside `Mapping/` and `Persistence/`, the boundary held.

The mapper throws on an unknown status and on a `NULL` in a column the current status requires,
rather than defaulting. Both cases mean the row and the schema disagree, and silently coercing
that to `Active` would hand out a usable session for an account whose real state is unknown.

## Trade-offs accepted

- **A corrupt row becomes a thrown exception during a query**, not a constraint violation on
  write. `ApplyToEntity` clears every stage column before writing, so a row cannot normally get
  into that state through the app, but a hand-edited row or a `DEFAULT` added by a DBA would
  surface as a `500` on read. The alternative — mapping it to a defensive placeholder stage —
  would be inventing a lifecycle stage that no business event backs.
- **No EF-level optimistic concurrency.** Nothing here is a `byte[] RowVersion`, so two
  concurrent failed sign-ins could lose an attempt count. That is a real bug for the sign-in
  path specifically, and it is deferred rather than solved: adding a rowversion means deciding
  what a lost update means (retry? or let the next sign-in re-read?). Worth doing before any
  auth endpoint ships.
- **Roles are a JSON string column, not a join table.** Role membership is never queried
  ("all auditors") and is written rarely, so a JSON column avoids a table and a second
  aggregate. The cost is that a future "find all users with role X" query needs
  `OPENJSON`/`json_each` and eventually an index, or a migration to a join table.
- **`SaveAsync` is one `SaveChanges` per call**, so a transition that needs to touch more than
  one aggregate is two calls and therefore two transactions. That is acceptable now because
  every transition in this slice writes exactly one row; the first time a rule spans two
  aggregates, the Application layer has to open an explicit transaction, and this note is the
  place that assumption is written down.
- **`dotnet ef` runs from the `Infrastructure` project, not `Api`.** A design-time
  `IDbContextFactory` supplies a placeholder connection string so scaffolding a migration needs
  neither a configured connection string nor a running web host. The cost is that the command
  needs `--project Infrastructure` if invoked from the solution root, which is easy to get wrong
  and produces a confusing "startup project doesn't reference
  Microsoft.EntityFrameworkCore.Design" error.

## Revisit when

- A second query needs to filter on lifecycle stage from inside a handler rather than through
  the repository. Today `IUserRepository` deliberately exposes no `FindByStatus`, because a
  handler switching on status is the leak this design exists to prevent. If a real use case
  needs it, add a purpose-named method (`FindLockedOutAsync`) rather than a generic status
  filter, and expect the temptation to grow from there.
- Password reset, refresh tokens, or email-change confirmation need per-token expiry and
  single-use semantics. Those are naturally a separate `AccountToken` aggregate with its own
  table rather than more nullable columns on `Users`, because a token has a lifecycle
  independent of the account's.
- Concurrent sign-in is actually implemented. Add a rowversion then, and decide the
  lost-update policy at the same time.
