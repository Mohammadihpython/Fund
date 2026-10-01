namespace Application.Users.Commands;

/// <summary>The token from the emailed verification link, plus the address it was sent to.</summary>
public sealed record VerifyEmailCommand(string Email, string Token);

/// <summary>
/// Asks for a new verification email. Always reports success to the caller, whether or not an
/// account exists — see the note in <c>ResendVerificationHandler</c>.
/// </summary>
public sealed record ResendVerificationCommand(string Email);