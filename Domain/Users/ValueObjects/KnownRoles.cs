namespace Domain.Users;

/// <summary>
/// The roles this system knows about. A static list of known names, not an enum —
/// the set lives in data, and these are simply the ones the product has decided on.
/// </summary>
public static class KnownRoles
{
    public static readonly Role Donor = Role.FromTrustedSource("donor");
    public static readonly Role CharityAdmin = Role.FromTrustedSource("charity-admin");
    public static readonly Role PlatformAdmin = Role.FromTrustedSource("platform-admin");
    public static readonly Role Auditor = Role.FromTrustedSource("auditor");
}
