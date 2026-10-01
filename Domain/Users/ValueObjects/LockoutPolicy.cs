using SharedKernel;

namespace Domain.Users;

/// <summary>
/// How many failed sign-ins it takes to lock an account, and for how long.
///
/// This is a value object rather than two fields on the user because the threshold is a
/// *policy*, not a property of any one account: the same rule applies to every user, it
/// changes over time, and storing it per-row would let two users disagree about it.
/// Passing it into the transition (rather than reading it from a static) also keeps the
/// transition a pure function, so a test can exercise the lockout window in milliseconds.
/// </summary>
public sealed record LockoutPolicy
{
    public int MaxFailedAttempts { get; }

    public TimeSpan LockoutDuration { get; }

    private LockoutPolicy(int maxFailedAttempts, TimeSpan lockoutDuration)
    {
        MaxFailedAttempts = maxFailedAttempts;
        LockoutDuration = lockoutDuration;
    }

    public static Result<LockoutPolicy> Create(int maxFailedAttempts, TimeSpan lockoutDuration)
    {
        if (maxFailedAttempts < 1)
            return Result.Failure<LockoutPolicy>(Error.Validation(
                "LockoutPolicy.MaxFailedAttempts",
                "An account must be allowed at least one sign-in attempt."));

        if (lockoutDuration <= TimeSpan.Zero)
            return Result.Failure<LockoutPolicy>(Error.Validation(
                "LockoutPolicy.LockoutDuration",
                "Lockout duration must be a positive span of time."));

        return Result.Success(new LockoutPolicy(maxFailedAttempts, lockoutDuration));
    }

    public static LockoutPolicy FromTrustedSource(int maxFailedAttempts, TimeSpan lockoutDuration) =>
        new(maxFailedAttempts, lockoutDuration);

    /// <summary>Five failures, fifteen minutes. Deliberately conservative for a money-handling product.</summary>
    public static LockoutPolicy Default { get; } = FromTrustedSource(5, TimeSpan.FromMinutes(15));

    public bool ShouldLockOut(int failedAttempts) => failedAttempts >= MaxFailedAttempts;
}
