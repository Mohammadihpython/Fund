using Domain.Users;

namespace Application.Users.Ports;

/// <summary>
/// Delivers the one message that leaves the system during signup: the email-verification link.
/// <para>
/// A port, not a service call inline in a handler, for two reasons. The handler stays testable
/// without a mail server, and — the reason that matters for a fundraising platform — swapping
/// providers (SMTP, a queue, a third-party API) is a DI change rather than a change to the
/// signup flow. It also keeps PII out of the handler: nothing here learns the user's address,
/// it only relays one that the domain already validated.
/// </para>
/// </summary>
public interface IEmailSender
{
    Task SendVerificationEmailAsync(Email to, string verificationToken, CancellationToken ct = default);
}