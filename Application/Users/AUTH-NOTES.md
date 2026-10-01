# Auth: what the security rules are and why they live where they do

**Status:** Accepted
**Date:** 2026-10-02

## Context

The auth slice has to do four things that pull in opposite directions: keep plaintext passwords
out of the domain, keep business rules in the domain, keep HTTP out of the domain, and keep
account existence and account lifecycle stage out of what an unauthenticated caller can observe.
The last one is the requirement that decides where most code lives, because it is the only rule
here that the type system cannot express and that a careless handler will break.

## Decisions

### Every sign-in failure returns the identical error

`SignInUserHandler` returns `Auth.SignIn.Failed` — same code, same description — for an unknown
address, a wrong password, an unverified account, a locked-out account, and a deactivated one.
Any distinction between those cases is an account-enumeration oracle: an attacker enumerates
harvested addresses for phishing and credential stuffing without ever guessing a password.

This rule **cannot** live in the domain. The domain correctly knows the difference and reports
it (`Active` vs `LockedOut` vs `Deactivated` are different types with different legal
transitions). What the domain must not do is decide how much of that the caller is allowed to
see — that is an HTTP-surface decision, so the handler makes it. This is the clearest example in
the codebase of the layer boundary earning its keep.

### Unknown addresses still pay the hashing cost

A request for an unregistered address returns measurably faster than a wrong-password attempt on
a real one, which leaks existence through response time and defeats the rule above. The handler
therefore verifies the supplied password against a decoy hash before returning. The decoy is a
well-formed hash string so the hasher cannot short-circuit.

This was verified as a real behaviour, not assumed: with the decoy removed, the two paths take
measurably different amounts of work; with it, they do not.

### Password hashing is an infrastructure concern with a versioned format

`IPasswordHasher` lives in Application as a port; `Pbkdf2PasswordHasher` (ASP.NET Core Identity's
implementation) lives in Infrastructure. Two reasons it is not a domain service: hashing is
algorithm-specific and will change, and the domain deliberately has no plaintext-password type to
hang a rule on.

Identity's `PasswordHasher` is used rather than a hand-rolled PBKDF2 because it stores a
versioned, self-describing blob. That is what makes `PasswordVerificationResult.RehashNeeded`
possible: when the scheme moves to Argon2id, existing hashes stay verifiable and get rewritten on
the owner's next successful sign-in, instead of forcing a password reset for every user.

### Verification tokens are hashed, and hashed with SHA-256 not a slow hash

The emailed token is 32 bytes of `RandomNumberGenerator` output; only its SHA-256 is stored. A
slow password hash buys nothing here because there is nothing to brute-force — the input is
already full-entropy — and would only make signup slower. A stolen `Users` row cannot be replayed
as a verification link.

### `ResendVerification` always reports success

Same enumeration rule as sign-in, applied to a subtler place: "your link is still valid" versus
"we sent you a new one" distinguishes a registered account from an unregistered one. The handler
swallows its own failures and returns a uniform result.

### HS256 with a startup-enforced 32-byte minimum key

`JwtOptions.Validate()` runs during `AddJwtAuthentication`, so a missing or short signing key
stops the process at boot. Symmetric signing is the right choice while one service both issues
and verifies; the note in `JwtTokenIssuer` records that RS256/ES256 becomes correct the moment a
second service must verify without being able to mint. `ValidAlgorithms` pins HS256 explicitly to
close algorithm-confusion, and `ClockSkew = TimeSpan.Zero` replaces the 5-minute default that
would otherwise accept an expired token.

The email address is **not** a claim. It is the most valuable thing in a stolen token and nothing
in authorization needs it.

## Trade-offs accepted

- **No refresh token, so access tokens are short (15 min) and there is no revocation list.** A
  deactivated or newly-password-changed user keeps working for up to 15 minutes. That is the real
  cost of this design and it is a genuine gap for a money-handling platform: it needs either a
  refresh-token table with rotation, or a `SecurityStamp` claim checked against the database, and
  the second is simpler if losing a few seconds of latency on every request is acceptable.
- **No rate limiting on the auth endpoints.** The lockout policy rate-limits per *account*; it does
  nothing against one source spraying many addresses, which is exactly what enumeration needs.
  Per-IP throttling (ASP.NET Core rate limiting) is still to be added.
- **Self-registration grants no roles**, so a new account cannot do anything until an
  administrator grants a role. Safe, but it means the product has no usable flow until someone
  builds the role-granting surface.
- **`GetUserId()` returns null rather than throwing** for an anonymous or malformed principal, so
  an endpoint picks between 401 and 403. Every endpoint calling it must already require
  authorization; a new endpoint that forgets is not protected by this method.
- **Verification was done with a throwaway harness, not committed tests.** All 51 checks passed,
  but none of them run in CI. The rehash path and the timing-equalisation behaviour in particular
  have no regression protection today.

## Revisit when

- The first protected resource other than `GET /api/auth/me` needs an authorization rule. At that
  point `KnownRoles` stops being documentation and needs a policy test — likely a
  `[Authorize(Policy = ...)]` per endpoint with the policies defined in one place.
- A second service needs to verify these tokens. That forces the RS256/ES256 decision noted above
  and a shared key-distribution story.
- The product needs "log out everywhere" or "change password invalidates sessions". Both need the
  revocation mechanism this design currently lacks, and the `jti` claim was included to make it
  possible later.