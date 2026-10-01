namespace Infrastructure.Persistence.Entities;

/// <summary>
/// The persistence-only mirror of the user lifecycle.
/// <para>
/// This enum plus the nullable columns below is exactly the shape the domain refuses to use —
/// and that is the point. A database row genuinely is "one shape, some columns unused right
/// now", and a query has to be able to say <c>WHERE Status = 'LockedOut'</c>. The domain's
/// sealed-record hierarchy stays the only place business rules are expressed; the only code
/// allowed to translate between the two is <c>UserMapper</c>.
/// </para>
/// <para>
/// Nothing outside <c>Infrastructure/Persistence</c> and <c>Infrastructure/Mapping</c> may
/// reference this type. If a handler needs to know the account's stage, it is holding a
/// <c>UserState</c> and should be switching on that instead.
/// </para>
/// </summary>
public enum UserStatus
{
    PendingVerification = 0,
    Active = 1,
    LockedOut = 2,
    Deactivated = 3
}
