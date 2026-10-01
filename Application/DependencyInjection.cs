using Application.Users;
using Application.Users.Commands;
using Application.Users.Handlers;
using Application.Users.Tokens;
using Domain.Users;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel;

namespace Application;

/// <summary>
/// Registers the handlers and the ports they need. The Api composition root calls this; it
/// registers no EF, no JWT library, and no HTTP types.
/// <para>
/// <see cref="TimeProvider"/> is bound to the system clock so handlers can be tested against a
/// <c>FakeTimeProvider</c> without an interface of our own — the lockout and token-expiry rules
/// are all time-dependent, so "advance the clock" has to be possible.
/// </para>
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        // Explicit type argument: LockoutPolicy is a readonly record struct, and both
        // AddSingleton overloads require a reference type without one.
        services.AddSingleton<LockoutPolicy>(LockoutPolicy.Default);

        services.AddScoped<ICommandHandler<RegisterUserCommand, Result<UserId>>, RegisterUserHandler>();
        services.AddScoped<ICommandHandler<VerifyEmailCommand, Result<SignInResult>>, VerifyEmailHandler>();
        services.AddScoped<ICommandHandler<ResendVerificationCommand, Result<ResendVerificationResult>>, ResendVerificationHandler>();
        services.AddScoped<ICommandHandler<SignInUserCommand, Result<SignInResult>>, SignInUserHandler>();

        return services;
    }
}