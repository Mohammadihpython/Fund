using Application.Users;
using Application.Users.Commands;
using Application.Users.Handlers;
using Application.Users.Tokens;
using Domain.Users;
using Infrastructure.Security;
using SharedKernel;

namespace Api.Endpoints;

/// <summary>
/// The auth surface.
/// <para>
/// Every endpoint in the group is anonymous, because the group's entire purpose is to obtain a
/// token — requiring one to reach it would be circular. The one endpoint that <em>does</em>
/// require a bearer token, <c>GET /api/auth/me</c>, is mapped outside the group for exactly that
/// reason.
/// </para>
/// </summary>
public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth")
            .WithTags("Auth")
            .AllowAnonymous();

        group.MapPost("/register", async (
                RegisterRequest request,
                ICommandHandler<RegisterUserCommand, Result<UserId>> handler,
                HttpContext context,
                CancellationToken ct) =>
            {
                var result = await handler.Handle(new RegisterUserCommand(request.Email, request.Password), ct);

                return result.IsSuccess
                    ? Results.Created($"/api/auth/pending-verification/{result.Value.Value}", new { userId = result.Value.Value })
                    : result.ToHttpResult(context);
            })
            .WithName("RegisterUser")
            .WithSummary("Creates an account and emails a verification link.");

        group.MapPost("/verify-email", async (
                VerifyEmailRequest request,
                ICommandHandler<VerifyEmailCommand, Result<SignInResult>> handler,
                HttpContext context,
                JwtOptions jwt,
                CancellationToken ct) =>
            {
                var result = await handler.Handle(new VerifyEmailCommand(request.Email, request.Token), ct);

                return ToTokenResult(result, context, jwt.AccessTokenLifetimeMinutes);
            })
            .WithName("VerifyEmail")
            .WithSummary("Confirms an email address from the emailed token and returns an access token.");

        group.MapPost("/resend-verification", async (
                ResendVerificationRequest request,
                ICommandHandler<ResendVerificationCommand, Result<ResendVerificationResult>> handler,
                HttpContext context,
                CancellationToken ct) =>
            {
                var result = await handler.Handle(new ResendVerificationCommand(request.Email), ct);

                return result.ToHttpResult(context);
            })
            .WithName("ResendVerification")
            .WithSummary("Re-sends the verification link. Always succeeds, by design.");

        group.MapPost("/sign-in", async (
                SignInRequest request,
                ICommandHandler<SignInUserCommand, Result<SignInResult>> handler,
                HttpContext context,
                JwtOptions jwt,
                CancellationToken ct) =>
            {
                var result = await handler.Handle(new SignInUserCommand(request.Email, request.Password), ct);

                return ToTokenResult(result, context, jwt.AccessTokenLifetimeMinutes);
            })
            .WithName("SignIn")
            .WithSummary("Exchanges a verified email and password for an access token.");

        // Outside the anonymous group: this is the endpoint that proves the token works.
        app.MapGet("/api/auth/me", (HttpContext context) =>
            {
                var user = context.User;

                return Results.Ok(new
                {
                    userId = user.GetUserId()?.Value,
                    roles = user.GetRoles(),
                    isAuthenticated = user.Identity?.IsAuthenticated ?? false
                });
            })
            .WithTags("Auth")
            .WithName("GetCurrentUser")
            .WithSummary("Returns the caller as described by their bearer token.")
            .RequireAuthorization();

        return app;
    }

    private static IResult ToTokenResult(
        Result<SignInResult> result,
        HttpContext context,
        int accessTokenLifetimeMinutes) =>
        result.IsSuccess
            ? result.Value.ToTokenResult(accessTokenLifetimeMinutes)
            : result.ToHttpResult(context);
}