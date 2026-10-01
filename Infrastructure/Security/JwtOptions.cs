using Microsoft.Extensions.Options;

namespace Infrastructure.Security;

/// <summary>
/// JWT settings, bound from configuration and validated at startup.
/// <para>
/// The whole point of <see cref="Validate"/> is to fail the process at boot rather than on the
/// first sign-in. A misconfigured signing key is the difference between "everyone can mint a
/// token for any account" and "the app refuses to start" — it must never be discovered
/// silently.
/// </para>
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>Minimum key length for HMAC-SHA256. Below this the MAC is weaker than the hash
    /// it protects, and .NET's own key-size validation will reject it at first use.</summary>
    public const int MinimumKeyLengthBytes = 32;

    public string Issuer { get; init; } = string.Empty;

    public string Audience { get; init; } = string.Empty;

    /// <summary>Base64-encoded signing key. Supplied by user-secrets locally and by environment
    /// variable when deployed — never committed.</summary>
    public string SigningKey { get; init; } = string.Empty;

    public int AccessTokenLifetimeMinutes { get; init; } = 15;

    public byte[] DecodedSigningKey =>
        Convert.FromBase64String(SigningKey);

    /// <summary>
    /// Decoded key length in bytes, or -1 when <see cref="SigningKey"/> is not valid base64.
    /// Measured from the decoded bytes rather than from the string length: base64 expands by 4/3,
    /// so a 44-character string is only 33 bytes and a length check on the string would be wrong.
    /// </summary>
    private int DecodedSigningKeyLength()
    {
        try
        {
            return Convert.FromBase64String(SigningKey).Length;
        }
        catch (FormatException)
        {
            return -1;
        }
    }

    public TimeSpan AccessTokenLifetime => TimeSpan.FromMinutes(AccessTokenLifetimeMinutes);

    public void Validate()
    {
        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(Issuer))
            failures.Add("Jwt:Issuer is required.");

        if (string.IsNullOrWhiteSpace(Audience))
            failures.Add("Jwt:Audience is required.");

        if (string.IsNullOrWhiteSpace(SigningKey))
        {
            failures.Add(
                "Jwt:SigningKey is required. Generate one for local development with " +
                "`dotnet user-secrets --project Api set \"Jwt:SigningKey\" \"$(openssl rand -base64 48)\"`.");
        }
        else if (DecodedSigningKeyLength() < MinimumKeyLengthBytes)
        {
            // Note the decode-then-measure order. Decode into a buffer sized to the *string*, not
            // to the minimum: Convert.TryFromBase64String returns false when the buffer is too
            // small for the decoded output, so sizing it to MinimumKeyLengthBytes would reject
            // every key longer than that — the exact keys we want to accept.
            failures.Add(
                $"Jwt:SigningKey must be base64 and decode to at least {MinimumKeyLengthBytes} bytes. " +
                "A short key makes the signature forgeable.");
        }

        if (AccessTokenLifetimeMinutes is < 1 or > 1440)
            failures.Add("Jwt:AccessTokenLifetimeMinutes must be between 1 and 1440.");

        // OptionsValidationException is what ValidateOnStart turns into a host-startup failure,
        // so a bad key stops the process instead of surfacing on the first sign-in attempt.
        if (failures.Count > 0)
            throw new OptionsValidationException(
                nameof(JwtOptions),
                typeof(JwtOptions),
                failures);
    }
}