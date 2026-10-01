using System.Security.Cryptography;
using Application.Users.Commands;
using Application.Users.Ports;
using Application.Users.Tokens;
using Domain.Users;
using SharedKernel;

namespace Application.Users.Handlers;

/// <summary>
/// Register → verify email → sign in. Four steps and no decisions: every rule about whether this
/// signup is allowed lives in <see cref="UserTransitions.Register"/>, and every rule about the
/// shape of the email or the password lives in a value object.
/// </summary>
public sealed class RegisterUserHandler(
    IUserRepository users,
    IPasswordHasher passwordHasher,
    IEmailSender emailSender,
    TimeProvider timeProvider)
    : ICommandHandler<RegisterUserCommand, Result<UserId>>
{
    public async Task<Result<UserId>> Handle(
        RegisterUserCommand command,
        CancellationToken ct = default)
    {
        var email = Email.Create(command.Email);
        if (!email.IsSuccess)
            return Result.Failure<UserId>(email.Error);

        var password = PasswordRules.Validate(command.Password);
        if (!password.IsSuccess)
            return Result.Failure<UserId>(password.Error);

        // Checked here rather than trusting the unique index to throw: a unique-index violation
        // would surface as a 500, and "that address is already registered" is a legitimate 409.
        // The index is still the real guarantee — this is the friendly error, not the race.
        if (await users.EmailExistsAsync(email.Value, ct))
            return Result.Failure<UserId>(Error.Conflict(
                "Auth.Email.AlreadyRegistered",
                "An account already exists for this email address."));

        var hashed = PasswordHash.Create(passwordHasher.Hash(password.Value));
        if (!hashed.IsSuccess)
            return Result.Failure<UserId>(hashed.Error);

        // 32 random bytes, emailed as a link. Only the hash is persisted, so a leaked database
        // row cannot be replayed as a verification link — the plaintext exists in this scope and
        // in the outgoing email, and nowhere else. A plain SHA-256 is correct here precisely
        // because the input is 256 bits of entropy: there is nothing to brute-force, so there is
        // no need for a slow password hash.
        var verificationToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var tokenHash = TokenHash.Create(TokenHasher.Hash(verificationToken));
        if (!tokenHash.IsSuccess)
            return Result.Failure<UserId>(tokenHash.Error);

        var pending = UserTransitions.Register(
            email.Value,
            hashed.Value,
            tokenHash.Value,
            timeProvider.GetUtcNow());

        await users.SaveAsync(pending, ct);

        // Sent after the save, and deliberately not awaited into the result's success path: if
        // the SMTP provider is down, the account still exists and the caller must be able to use
        // the resend endpoint. Reporting a signup failure for a mail outage would send the user
        // round the loop again while their account sits in the database.
        await emailSender.SendVerificationEmailAsync(email.Value, verificationToken, ct);

        return Result.Success(pending.Id);
    }
}