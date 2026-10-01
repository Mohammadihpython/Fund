namespace Application.Users.Tokens;

/// <summary>
/// What a successful sign-in or email verification hands back. The access token and its
/// expiry are returned together so the client can refresh on its own schedule instead of
/// decoding the token to find out when it dies.
/// </summary>
public sealed record SignInResult(string AccessToken, DateTimeOffset ExpiresAt, string Email);
