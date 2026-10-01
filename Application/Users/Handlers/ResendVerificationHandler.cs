using System.Security.Cryptography;
using Application.Users.Commands;
using Application.Users.Ports;
using Application.Users.Tokens;
using Domain.Users;
using SharedKernel;

namespace Application.Users.Handlers;

/// <summary>
/// Re-issue an email-verification token for an account that never confirmed.
/// <para>
/// The one handler whose result is deliberately not the truth: it returns success whether or not
/// the address is registered, and whether or not the previous token is still valid. Any other
/// behaviour turns "resend my link" into an account-existence oracle, which is the same leak the
/// sign-in handler goes to such lengths to avoid. The real reason a resend can fail — the address
/// is not registered — reaches the legitimate user as "if that account exists, a new link is on its
/// way", which is exactly as actionable.
/// </para>
/// </summary>
public sealed class ResendVerificationHandler(
    IUserRepository users,
    IEmailSender emailSender,
    TimeProvider timeProvider)
    : ICommandHandler<ResendVerificationCommand, Result<ResendVerificationResult>>
{
    public async Task<Result<ResendVerificationResult>> Handle(
        ResendVerificationCommand command,
        CancellationToken ct = default)
    {
        var now = timeProvider.GetUtcNow();

        var email = Email.Create(command.Email);
        if (!email.IsSuccess)
            return Result.Failure<ResendVerificationResult>(email.Error);

        var user = await users.FindByEmailAsync(email.Value, ct);

        if (user is not PendingVerification pending)
            return Result.Success(new ResendVerificationResult(Delivered: false));

        var verificationToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var tokenHash = TokenHash.Create(TokenHasher.Hash(verificationToken));
        if (!tokenHash.IsSuccess)
            return Result.Failure<ResendVerificationResult>(tokenHash.Error);

        // The domain rejects this while the previous link is still live — a second live token
        // would let anyone who intercepted the first email still activate the account. The
        // failure is swallowed for the same reason the result is always success: telling the
        // caller "your token is still valid" is an existence oracle.
        var resent = UserTransitions.ResendVerificationToken(pending, tokenHash.Value, now);
        if (!resent.IsSuccess)
            return Result.Success(new ResendVerificationResult(Delivered: false));

        await users.SaveAsync(resent.Value, ct);
        await emailSender.SendVerificationEmailAsync(email.Value, verificationToken, ct);

        return Result.Success(new ResendVerificationResult(Delivered: true));
    }
}

public sealed record ResendVerificationResult(bool Delivered);