using System.Security.Claims;
using Application.Users.Tokens;
using SharedKernel;

namespace Api.Endpoints;

/// <summary>
/// Applies a domain <see cref="Result"/> to an <see cref="IResult"/>.
/// <para>
/// The HTTP mapping lives here, in the Api layer, and not in the domain: choosing that a
/// <c>Conflict</c> error becomes <c>409</c> is an HTTP decision, and a domain type that needed
/// <c>Microsoft.AspNetCore.Http</c> would be a layer violation.
/// </para>
/// <para>
/// <see cref="ResultExtensions.ToResponse{T}"/> already builds the body (status code, message,
/// error description, trace id); this applies the status code to the response as well, which is
/// what actually makes it a <c>409</c> rather than a <c>200</c> carrying the number in a field.
/// </para>
/// </summary>
public static class ResultExtensions
{
    public static IResult ToHttpResult<T>(this Result<T> result, HttpContext context)
    {
        var response = result.ToResponse(context.TraceIdentifier);

        return Results.Json(response, statusCode: response.StatusCode);
    }

    /// <summary>
    /// Success shape for a token response. Kept separate from the error mapping so the two
    /// response shapes cannot drift into sharing fields they should not have.
    /// </summary>
    public static IResult ToTokenResult(this SignInResult result, int accessTokenLifetimeMinutes) =>
        Results.Ok(new AccessTokenResponse(
            AccessToken: result.AccessToken,
            TokenType: "Bearer",
            // Seconds, per RFC 6750, so the client can schedule its own refresh without decoding
            // the JWT to read `exp`. Derived from the same option the token was issued with.
            ExpiresIn: accessTokenLifetimeMinutes * 60,
            Email: result.Email));
}

/// <summary>
/// Reads the authenticated caller out of the bearer token.
/// <para>
/// The endpoint layer is the only place that knows the token was a JWT, which is the point: a
/// handler asks "who is this?" through this abstraction and never parses a claim itself. If the
/// token format changes, this is the only file that changes.
/// </para>
/// </summary>
public static class CurrentUserExtensions
{
    /// <summary>
    /// The caller's <c>UserId</c>, or <c>null</c> for an anonymous request. Returns null rather
    /// than throwing so an endpoint can decide between 401 and 403; only endpoints that already
    /// require authorization should call it.
    /// </summary>
    public static Domain.Users.UserId? GetUserId(this ClaimsPrincipal principal)
    {
        var raw = principal.FindFirst("sub")?.Value;

        return Guid.TryParse(raw, out var id)
            ? Domain.Users.UserId.FromTrustedSource(id)
            : null;
    }

    public static IReadOnlyList<string> GetRoles(this ClaimsPrincipal principal) =>
        principal.FindAll("role").Select(c => c.Value).ToList();
}