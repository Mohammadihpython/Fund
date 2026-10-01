using SharedKernel;

namespace Domain.Users;

/// <summary>
/// Past-tense facts about the user aggregate. Application handlers publish these only
/// after the state has been saved, so a subscriber can never observe an event for a
/// transition that was rolled back.
/// </summary>
public abstract record UserEvent(UserId UserId, DateTimeOffset OccurredAt);

public sealed record UserRegistered(
    UserId UserId,
    Email Email,
    DateTimeOffset OccurredAt) : UserEvent(UserId, OccurredAt);

public sealed record UserEmailVerified(UserId UserId, DateTimeOffset OccurredAt)
    : UserEvent(UserId, OccurredAt);

public sealed record UserSignedIn(UserId UserId, DateTimeOffset OccurredAt)
    : UserEvent(UserId, OccurredAt);

/// <summary>Carries no address or failure count on purpose: publishing "user X failed to
/// sign in from IP Y" turns the event stream into a credential-stuffing target for anything
/// that can read it.</summary>
public sealed record UserLockedOut(UserId UserId, DateTimeOffset OccurredAt)
    : UserEvent(UserId, OccurredAt);

public sealed record UserUnlocked(UserId UserId, DateTimeOffset OccurredAt)
    : UserEvent(UserId, OccurredAt);

public sealed record UserDeactivated(UserId UserId, string Reason, DateTimeOffset OccurredAt)
    : UserEvent(UserId, OccurredAt);

public sealed record UserReactivated(UserId UserId, DateTimeOffset OccurredAt)
    : UserEvent(UserId, OccurredAt);

public sealed record UserPasswordChanged(UserId UserId, DateTimeOffset OccurredAt)
    : UserEvent(UserId, OccurredAt);
