using SharedKernel;

namespace Domain.Users;

/// <summary>
/// A role, parsed from whatever the caller had (a claim, a database string, a config
/// key). Kept as a value object rather than an enum so adding a role is a data change,
/// not a code + migration change.
/// </summary>
public readonly record struct Role
{
    public string Value { get; }

    private Role(string value) => Value = value;

    public static Result<Role> Create(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? Result.Failure<Role>(Error.Validation(
                "Role.Required",
                "Role name is required."))
            : Result.Success(new Role(value.Trim().ToLowerInvariant()));

    public static Role FromTrustedSource(string value) => new(value);

    public override string ToString() => Value;
}
