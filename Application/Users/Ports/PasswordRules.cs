using SharedKernel;

namespace Application.Users.Ports;

/// <summary>
/// Checks the raw password string that arrived from HTTP, before it is hashed.
/// <para>
/// This lives in the Application layer, not the domain, and the reason is structural rather than
/// stylistic: the domain has no plaintext-password type at all (see
/// <c>Domain.Users.PasswordHash</c>), so a length rule cannot be expressed as an invariant of a
/// domain type. The rule still has to exist, and it has to exist at the one place the plaintext
/// is in scope. Note that email validation is deliberately <em>not</em> duplicated here — that one
/// is a domain invariant on <c>Email</c>, which is why <c>RegisterUserCommand</c> only needs a
/// password check.
/// </para>
/// </summary>
public static class PasswordRules
{
    /// <summary>Long, because length beats character-class rules: NIST SP 800-63B advises
    /// against mandatory composition rules and for a minimum of 8 (here 12, for a money-handling
    /// product) checked against a breached-password list.</summary>
    public const int MinimumLength = 12;

    /// <summary>An upper bound is a denial-of-service control, not a security control: hashing
    /// is deliberately expensive per byte, so an unbounded 10 MB "password" is a cheap way to
    /// make the server busy.</summary>
    public const int MaximumLength = 128;

    public static Result<string> Validate(string? plaintextPassword)
    {
        if (string.IsNullOrWhiteSpace(plaintextPassword))
            return Result.Failure<string>(Error.Validation(
                "Auth.Password.Required",
                "Password is required."));

        if (plaintextPassword.Length > MaximumLength)
            return Result.Failure<string>(Error.Validation(
                "Auth.Password.TooLong",
                $"Password cannot exceed {MaximumLength} characters."));

        if (plaintextPassword.Length < MinimumLength)
            return Result.Failure<string>(Error.Validation(
                "Auth.Password.TooShort",
                $"Password must be at least {MinimumLength} characters."));

        return Result.Success(plaintextPassword);
    }
}