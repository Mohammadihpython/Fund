# Identity: user lifecycle as a state hierarchy

**Status:** Accepted
**Date:** 2026-10-01

## Context

Identity is the first vertical slice, and the lifecycle is not cosmetic: which operations are
legal depends entirely on the account's stage. A user who has not verified their email must
not be able to sign in; a locked-out user must not be able to sign in; a deactivated user must
not be able to sign in. With one `User` class and a `UserStatus` enum, all three of those rules
become `if` statements a caller can forget, and the compiler catches none of them.

## Options considered

- **Option A** — one `User` class with a `UserStatus` enum plus nullable fields for whatever
  applies at the current stage (`VerificationToken?`, `LockedUntil?`, `DeactivatedAt?`).
- **Option B** — abstract `UserState` record with one `sealed record` per stage, and static
  transitions that take the specific predecessor type as their parameter.

## Decision

Option B. The deciding factor is that a transition's *parameter type* is what makes an illegal
call uncompilable. `UserTransitions.Deactivate` has overloads for `Active`, `LockedOut`, and
`PendingVerification` but not `Deactivated` — so deactivating an already-deactivated account is
a `CS1503` build error, not a runtime guard someone has to remember. `CS8509`/`CS8524` are
promoted to errors in `Domain.csproj` so a `switch` over `UserState` that misses a new stage also
fails the build; a `_ =>` arm is still an explicit opt-out if some caller genuinely needs one.

## Trade-offs accepted

- **The domain grew before any consumer existed.** Nothing in the solution calls these
  transitions yet, so the first review of this code has no real usage to check it against. In
  exchange, the handler layer (Application, not yet created) gets written as a table of contents
  with zero `if` statements in it.
- **Two rules stay runtime checks, deliberately.** Token expiry and the lockout window depend
  on *time*, which the type system cannot know — `Verify(expiredToken, now)` and
  `Unlock(locked, tooEarly)` must return `Result`. Everything else is a compile error instead.
- **`RecordFailedSignIn` returns the abstract `UserState`, not a single concrete type.** Its
  result genuinely depends on the attempt count, and C# 14 has no discriminated union to express
  "either `Active` or `LockedOut`". The caller narrows with a `switch`; the `CS8509` guard
  applies there too, so a new stage cannot be silently mishandled.
- **`Deactivated` records `EmailVerifiedAt` but a never-verified account has nothing to record**,
  so `Deactivate(PendingVerification, ...)` passes `RegisteredAt` as a stand-in. Slightly
  dishonest. Left as-is because the alternative — a nullable field or a second never-verified
  variant — is worse; worth revisiting if audit ever needs to tell the two apart.
- **`Role` is a value object wrapping a string, not an enum.** Adding a role is then a data
  change rather than a code change plus a migration, at the cost of losing compile-time
  completeness over the role set. Acceptable: roles are read from claims and config, so the set
  is genuinely data-shaped.
- **`UserState.Roles` is a `UserRoles`, not an `ImmutableArray<Role>` — this one was a real bug.**
  `ImmutableArray<T>` has no structural equality (it inherits `Equals` from its underlying
  array), so `record` equality ignored it entirely: two `UserState`s holding the same roles in
  the same order compared **unequal**. Nothing failed loudly — a mapper round-trip test just
  reported `equal=False` with every visible field matching, and any test asserting equality, or
  any event dedup or cache key using it, would have silently stopped working. `UserRoles`
  wraps the collection in a record that compares with `SetEquals`. The general rule this
  establishes: **a `record` field of a collection type needs a wrapper if anything will ever
  compare that record.** `ImmutableList<T>` and `ImmutableHashSet<T>` fail the same way; a
  joined string would have worked but throws away the types.

## Revisit when

- A stage needs data that would be awkward to carry forward through every transition (e.g. a
  `Suspended` stage distinct from `Deactivated`, with its own `SuspendedUntil` and approver).
  At that point the "carry the fields forward" approach in each transition starts duplicating
  more than a shared helper is worth, and the per-stage construction should move to a factory.
- An `Application` project exists, at which point the architecture test from
  `references/testing-and-checks.md` should be added to pin `Users.Domain → SharedKernel` and
  nothing else.
- The solution layout is per-layer (`Domain`, `Infrastructure`), not per-feature. See the repo
  root `AGENTS.md` for the authoritative map; the layout has now changed twice in this repo
  (flat `FallahFund.*` → short names → vertical slices → back), so trust `FallahFund .sln` over
  any prose describing it.
