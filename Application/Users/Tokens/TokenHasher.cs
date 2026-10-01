using System.Security.Cryptography;
using System.Text;

namespace Application.Users.Tokens;

/// <summary>
/// Hashes a raw single-use token before it is allowed anywhere near the domain.
/// <para>
/// Shared by registration and email verification because both mint the same kind of secret, and
/// the hashing has to be identical on both sides or a token will never verify.
/// </para>
/// <para>
/// A plain SHA-256 is the right function here, which is worth stating because it is the wrong
/// function for passwords. There is nothing to brute-force: the input is 256 bits of
/// <c>RandomNumberGenerator</c> output, so the only way to produce a matching digest is to
/// already know the token. A deliberately slow hash buys no security against a full-entropy
/// random value and would only make the signup path slower.
/// </para>
/// </summary>
internal static class TokenHasher
{
    internal static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token ?? string.Empty)));
}