using SharedKernel;

namespace Domain.Users;

/// <summary>
/// A hash of a single-use token (email verification, password reset). The plaintext
/// token exists only in the email we send out; from that point the domain can only
/// ever compare hashes, so a leaked database row cannot be replayed as a login link.
/// </summary>
public readonly record struct TokenHash
{
    public string Value { get; }

    private TokenHash(string value) => Value = value;

    public static Result<TokenHash> Create(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? Result.Failure<TokenHash>(Error.Validation(
                "TokenHash.Required",
                "Token hash is required. Store a hash of the token, never the token itself."))
            : Result.Success(new TokenHash(value));

    public static TokenHash FromTrustedSource(string value) => new(value);

    public override string ToString() => "***";
}
