using SharedKernel;

namespace Domain.Users;

/// <summary>
/// A user's email address. Normalized to lower-case on the way in, so
/// "Ali@Example.com" and "ali@example.com" are the same account rather than two.
/// </summary>
public readonly record struct Email
{
    public const int MaxLength = 254;

    public string Value { get; }

    private Email(string value) => Value = value;

    public static Result<Email> Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Invalid("Email.Required", "Email is required.");

        var normalized = value.Trim().ToLowerInvariant();

        if (normalized.Length > MaxLength)
            return Invalid("Email.TooLong", $"Email cannot exceed {MaxLength} characters.");

        // Deliberately a shape check, not a full RFC 5322 parse: the domain rule we
        // actually care about is "one @, something either side, a dotted domain".
        // Over-strict regexes reject addresses that are legal and deliverable.
        var at = normalized.IndexOf('@');
        if (at <= 0 || at != normalized.LastIndexOf('@') || at == normalized.Length - 1)
            return Invalid("Email.Malformed", "Email must contain exactly one '@' with text on both sides.");

        var domain = normalized[(at + 1)..];
        if (!domain.Contains('.') || domain.StartsWith('.') || domain.EndsWith('.'))
            return Invalid("Email.Malformed", "Email domain must be dotted, e.g. example.com.");

        if (normalized.Any(char.IsWhiteSpace))
            return Invalid("Email.Malformed", "Email cannot contain whitespace.");

        return Result.Success(new Email(normalized));
    }

    public static Email FromTrustedSource(string value) => new(value);

    private static Result<Email> Invalid(string code, string description) =>
        Result.Failure<Email>(Error.Validation(code, description));

    public override string ToString() => Value;
}
