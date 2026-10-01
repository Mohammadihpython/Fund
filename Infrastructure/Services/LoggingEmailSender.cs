using Application.Users.Ports;
using Domain.Users;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services;

/// <summary>
/// Development stand-in for real email delivery. Writes the verification token to the log rather
/// than sending it, because there is no SMTP provider configured for this environment.
/// <para>
/// This is a placeholder and should be treated as one: a real implementation belongs behind the
/// same <see cref="IEmailSender"/> port. Note that it logs the token in plaintext — acceptable
/// only because the alternative is no way to complete a signup locally, and only because this is
/// never registered outside Development.
/// </para>
/// </summary>
public sealed class LoggingEmailSender(ILogger<LoggingEmailSender> logger) : IEmailSender
{
    public Task SendVerificationEmailAsync(Email to, string verificationToken, CancellationToken ct = default)
    {
        logger.LogWarning(
            "DEVELOPMENT ONLY: verification email for {Email} would be sent. Token: {Token}",
            to.Value,
            verificationToken);

        return Task.CompletedTask;
    }
}