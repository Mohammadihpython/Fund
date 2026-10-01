using SharedKernel;

namespace Domain.Users;

/// <summary>
/// A stored password hash. The plaintext password is never a domain type — it exists
/// only in the request DTO, is hashed at the boundary, and from that moment the
/// domain only ever sees this. That is what keeps "log the user object" from leaking
/// a password, and it makes the absence of a plaintext field structural, not a
/// convention someone has to remember.
/// </summary>
public readonly record struct PasswordHash
{
    public string Value { get; }

    private PasswordHash(string value) => Value = value;

    public static Result<PasswordHash> Create(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? Result.Failure<PasswordHash>(Error.Validation(
                "PasswordHash.Required",
                "Password hash is required. Plaintext passwords must never reach the domain."))
            : Result.Success(new PasswordHash(value));

    public static PasswordHash FromTrustedSource(string value) => new(value);

    public override string ToString() => "***";
}
