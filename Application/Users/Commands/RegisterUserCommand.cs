namespace Application.Users.Commands;

/// <summary>
/// The signup request as it arrives from HTTP: raw strings, no domain types yet.
/// <para>
/// Deliberately does not contain roles. Self-service registration must not be able to grant
/// itself <c>platform-admin</c>, so a registered user starts with no roles and an administrator
/// grants them out of band. A <c>Roles</c> property here would be a privilege-escalation bug
/// waiting for someone to bind it from JSON.
/// </para>
/// </summary>
public sealed record RegisterUserCommand(string Email, string Password);