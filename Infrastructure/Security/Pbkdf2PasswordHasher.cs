using Application.Users.Ports;
using Microsoft.AspNetCore.Identity;

// Both sides define a PasswordVerificationResult. Aliased rather than renamed: the port's name
// is the one the rest of the solution speaks, and renaming a framework type here would be a
// permanent, surprising difference from the documentation everyone else reads.
using IdentityVerification = Microsoft.AspNetCore.Identity.PasswordVerificationResult;
using PortVerification = Application.Users.Ports.PasswordVerificationResult;

namespace Infrastructure.Security;

/// <summary>
/// Password hashing via ASP.NET Core Identity's <see cref="PasswordHasher{TUser}"/> — PBKDF2-HMAC-SHA256,
/// 100,000 iterations, per-password salt, verified in constant time.
/// <para>
/// Why this and not a hand-rolled <c>Rfc2898DeriveBytes</c>: the Identity implementation stores a
/// versioned, self-describing string (<c>$PBKDF2-...</c> plus the algorithm id), which is what makes
/// the <see cref="PasswordVerificationResult.RehashNeeded"/> path possible. When the scheme is
/// upgraded to Argon2id, existing hashes stay verifiable and get rewritten on the next successful
/// sign-in. A raw fixed-format digest would force a password reset for every user the day the
/// algorithm changed.
/// </para>
/// <para>
/// The static <c>PasswordHasher&lt;TUser&gt;</c> API does not use the <c>user</c> argument, so the
/// Application layer never has to pass a domain type into an infrastructure one.
/// </para>
/// </summary>
public sealed class Pbkdf2PasswordHasher : IPasswordHasher
{
    private readonly PasswordHasher<object> _hasher = new();

    public string Hash(string plaintextPassword) => _hasher.HashPassword(new object(), plaintextPassword);

    public PortVerification Verify(string plaintextPassword, string passwordHash)
    {
        // A null/blank stored hash means a corrupt or foreign row. Report it as a failed
        // verification rather than throwing: an exception here would become a 500 and tell an
        // attacker they had found something interesting.
        if (string.IsNullOrWhiteSpace(passwordHash))
            return PortVerification.Failed;

        try
        {
            return _hasher.VerifyHashedPassword(new object(), passwordHash, plaintextPassword) switch
            {
                IdentityVerification.Success => PortVerification.Success,
                IdentityVerification.SuccessRehashNeeded => PortVerification.RehashNeeded,
                _ => PortVerification.Failed
            };
        }
        catch (FormatException)
        {
            // The stored value is not a hash this hasher understands — e.g. a row written by a
            // different scheme. Treated as a failed login; the rehash path cannot help because we
            // cannot verify the user's password against it at all.
            return PortVerification.Failed;
        }
    }
}