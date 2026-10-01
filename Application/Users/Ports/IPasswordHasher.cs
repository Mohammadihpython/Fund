using Domain.Users;
using SharedKernel;

namespace Application.Users.Ports;

/// <summary>
/// Turns a plaintext password into the stored hash, and checks one against the other.
/// <para>
/// A port rather than a domain service on purpose: hashing is a CPU-costly, algorithm-specific
/// concern, and the domain refuses to even have a plaintext-password type (see
/// <c>Domain.Users.PasswordHash</c>). The algorithm is an infrastructure decision that will
/// change — Argon2id is the current recommendation, PBKDF2 is what most systems still hold, and
/// a rehash-on-verify path has to exist either way — so the choice does not belong next to the
/// business rules.
/// </para>
/// <para>
/// Implementations MUST use a salted, deliberately slow algorithm. Anything instant (SHA-256,
/// MD5) is not a password hash; it is a fast hash that an attacker with a stolen database can
/// crack at billions of guesses per second.
/// </para>
/// </summary>
public interface IPasswordHasher
{
    string Hash(string plaintextPassword);

    PasswordVerificationResult Verify(string plaintextPassword, string passwordHash);
}

/// <summary>
/// Why verification is three-valued and not a bool: <see cref="RehashNeeded"/> is how the
/// algorithm gets upgraded without invalidating anyone's password. When a hash was made with an
/// older/weaker scheme, verification still succeeds but the caller is told to re-hash with the
/// current one on the way past. A bool would force a "reset your password" email to every user
/// whenever the scheme changed.
/// </summary>
public enum PasswordVerificationResult
{
    Failed = 0,
    Success = 1,
    RehashNeeded = 2
}

/// <summary>
/// Issues the bearer token for an authenticated user.
/// <para>
/// The interface speaks in domain terms — it is handed a <c>UserId</c> and a set of roles — and
/// knows nothing about JWT. If the token format is later replaced (a reference token, a
/// Paseto, an opaque token plus a lookup), only this port's implementation changes.
/// </para>
/// </summary>
public interface IAccessTokenIssuer
{
    IssuedAccessToken Issue(UserId userId, IReadOnlyCollection<Role> roles);
}

public sealed record IssuedAccessToken(string Token, DateTimeOffset ExpiresAt);