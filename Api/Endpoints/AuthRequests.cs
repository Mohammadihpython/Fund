namespace Api.Endpoints;

/// <summary>
/// Request DTOs for the auth endpoints.
/// <para>
/// These are records of plain strings, and they are the only place a plaintext password is allowed
/// to exist. Model binding fills them from the JSON body; nothing downstream ever sees the type
/// again, and the handler works with the value rather than the object.
/// </para>
/// </summary>
public sealed record RegisterRequest(string Email, string Password);

public sealed record SignInRequest(string Email, string Password);

public sealed record VerifyEmailRequest(string Email, string Token);

public sealed record ResendVerificationRequest(string Email);

/// <summary>
/// What a client gets back after a successful sign-in or verification.
/// <summary>
/// <c>token_type</c> is not decoration: RFC 6750 requires it so a client knows to send the value
/// as <c>Authorization: Bearer &lt;token&gt;</c> rather than as some other scheme. <c>expires_in</c>
/// is in seconds because that is what the RFC specifies, and it lets a client schedule its own
/// refresh instead of decoding the JWT to read the <c>exp</c>.
/// </summary>
public sealed record AccessTokenResponse(
    string AccessToken,
    string TokenType,
    int ExpiresIn,
    string Email);