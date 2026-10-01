using SharedKernel;

namespace Domain.Users;

/// <summary>
/// Identity of a user. Distinct from every other id type in the system, so a
/// <c>UserId</c> can never be passed where a <c>CampaignId</c> is expected.
/// </summary>
public readonly record struct UserId
{
    public Guid Value { get; }

    private UserId(Guid value) => Value = value;

    public static UserId New() => new(Guid.CreateVersion7());

    public static Result<UserId> Create(Guid value) =>
        value == Guid.Empty
            ? Result.Failure<UserId>(Error.Validation(
                "UserId.Empty",
                "User id cannot be an empty guid."))
            : Result.Success(new UserId(value));

    public static UserId FromTrustedSource(Guid value) => new(value);

    public override string ToString() => Value.ToString();
}
