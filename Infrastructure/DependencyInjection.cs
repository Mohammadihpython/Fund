using Application.Users.Ports;
using Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Infrastructure;

/// <summary>
/// The only place that knows a database exists and the only place that knows a JWT exists. The Api
/// composition root calls this; it never sees EF types or token types.
/// </summary>
public static class DependencyInjection
{
    /// <param name="connectionString">
    /// Passed in rather than read from configuration here, so this project has no opinion about
    /// where the string comes from (user-secrets locally, environment variables deployed). The
    /// Api project reads it and supplies it.
    /// </param>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddDbContext<Persistence.FallahFundDbContext>(options =>
            options.UseSqlServer(connectionString));

        // The interface is the port, the implementation is the adapter. Registering it against
        // the interface is what lets Domain and Application stay free of EF entirely.
        services.AddScoped<Domain.Users.IUserRepository, Repositories.EfUserRepository>();

        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddScoped<IAccessTokenIssuer, JwtTokenIssuer>();

        return services;
    }

    /// <summary>
    /// Token authentication and authorization. Kept separate from <see cref="AddInfrastructure"/>
    /// because it is a security concern rather than a persistence one — and because a test that
    /// wants a DbContext without wanting bearer auth should not have to opt out of both.
    /// </summary>
    public static IServiceCollection AddJwtAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var jwt = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
            ?? throw new InvalidOperationException(
                $"Configuration section '{JwtOptions.SectionName}' is missing.");

        // Throws here rather than on the first request: an unset or short signing key must stop
        // the process, not quietly serve traffic.
        jwt.Validate();

        services.AddSingleton(Options.Create(jwt));

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                // Read "sub" and "role" as written instead of being rewritten to the long
                // Microsoft claim URIs. Both sides of this decision are in JwtTokenIssuer; if they
                // disagree, IsInRole silently matches nothing.
                options.MapInboundClaims = false;

                options.TokenValidationParameters = JwtTokenIssuer.ValidationParameters(jwt);
            });

        services.AddAuthorization();

        return services;
    }

    /// <summary>
    /// Development-only email. Guarded so the logging sender cannot be registered in production by
    /// accident — a missing production email provider should be a startup failure, not a silent
    /// switch to a sender that writes tokens to the log.
    /// </summary>
    public static IServiceCollection AddDevelopmentEmailSender(this IServiceCollection services) =>
        services.AddSingleton<IEmailSender, LoggingEmailSender>();
}