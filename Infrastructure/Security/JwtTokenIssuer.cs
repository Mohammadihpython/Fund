using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Application.Users.Ports;
using Domain.Users;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Infrastructure.Security;

/// <summary>
/// Issues the signed bearer token and puts the user's data in it.
/// <para>
/// Claims emitted, and why each earns its place:
/// <list type="bullet">
/// <item><c>sub</c> — the <c>UserId</c>. The only claim the application treats as identity.
/// Under JWT, <c>sub</c> is scoped to the *issuer*, never global, so it is safe to key data on
/// as long as the issuer is validated — which <c>ValidateIssuer</c> does.</item>
/// <item><c>jti</c> — a random token id. Not read by this application today; it exists so a
/// future deny-list (logout, "revoke this session", password-change invalidates all tokens) can
/// name a specific token rather than guessing at one.</item>
/// <item><c>iat</c> — issued-at, so a consumer can tell a fresh token from a stale one.</item>
/// <item><c>auth_time</c> — when the user actually authenticated, distinct from issue time, and
/// what a future step-up-auth check reads.</item>
/// <item><c>role</c> — one per role. Authorization reads these; the application never
/// re-queries the database to decide a permission.</item>
/// </list>
/// </para>
/// <para>
/// The email address is deliberately <b>not</b> a claim. It is PII, it is the value an attacker
/// most wants from a stolen token, and nothing in authorization needs it — the client already
/// knows what it submitted. If a display name is ever needed downstream it should come from the
/// database, which is also the only way to notice that a user changed it.
/// </para>
/// <para>
/// Lifetime is short (15 minutes by default) because there is no refresh token and no
/// revocation list yet. That is the honest trade: a short token means a deactivated user's access
/// ends within the lifetime rather than immediately. See the design note.
/// </para>
/// </summary>
public sealed class JwtTokenIssuer(IOptions<JwtOptions> options, TimeProvider timeProvider) : IAccessTokenIssuer
{
    private readonly JwtOptions _options = options.Value;

    public IssuedAccessToken Issue(UserId userId, IReadOnlyCollection<Role> roles)
    {
        var now = timeProvider.GetUtcNow();
        var expiresAt = now.Add(_options.AccessTokenLifetime);

        var claims = new List<Claim>
        {
            // Registered claim names, not the long ClaimTypes.* URIs. JwtSecurityTokenHandler maps
            // inbound claims by default, which turns "sub" into a long URI and then quietly
            // breaks a lookup by "sub". Set MapInboundClaims = false on the validator instead
            // (see AddAuthentication) and keep the token readable.
            new(JwtRegisteredClaimNames.Sub, userId.Value.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Iat,
                EpochTime.GetIntDate(now.UtcDateTime).ToString(),
                ClaimValueTypes.Integer64),
            new("auth_time",
                EpochTime.GetIntDate(now.UtcDateTime).ToString(),
                ClaimValueTypes.Integer64)
        };

        claims.AddRange(roles.Select(role => new Claim(JwtClaimTypes.Role, role.Value)));

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(_options.DecodedSigningKey),
            // HS256 rather than the RS256 default: a single service both signs and verifies, so a
            // symmetric key keeps the configuration to one secret. RS256/ES256 becomes the right
            // call the moment a second service needs to verify these tokens without being able to
            // mint them.
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: expiresAt.UtcDateTime,
            signingCredentials: credentials);

        return new IssuedAccessToken(new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }

    /// <summary>
    /// Claim names as they appear in the token. Kept as constants so the issuer and the validator
    /// cannot drift: with <c>MapInboundClaims = false</c> the inbound names are the short ones, so
    /// <c>RoleClaimType</c> must be "role" and not <c>ClaimTypes.Role</c> (the long
    /// <c>http://schemas.microsoft.com/...</c> URI) or <c>IsInRole</c> would never match anything.
    /// </summary>
    public static class JwtClaimTypes
    {
        public const string Subject = "sub";
        public const string Role = "role";
        public const string TokenId = "jti";
    }

    /// <summary>Shareable validation parameters, so issuing and validating cannot drift apart.</summary>
    public static TokenValidationParameters ValidationParameters(JwtOptions options) => new()
    {
        ValidateIssuer = true,
        ValidIssuer = options.Issuer,
        ValidateAudience = true,
        ValidAudience = options.Audience,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(options.DecodedSigningKey),
        ValidateLifetime = true,
        // Default is 5 minutes, which silently accepts a token that expired up to 5 minutes ago.
        // Zero means exactly-not-expired; clock skew is handled by the host's time sync instead.
        ClockSkew = TimeSpan.Zero,
        // Keep "sub" as "sub" and "role" as "role". See the note in Issue().
        NameClaimType = JwtClaimTypes.Subject,
        RoleClaimType = JwtClaimTypes.Role,
        ValidAlgorithms = [SecurityAlgorithms.HmacSha256]
    };
}