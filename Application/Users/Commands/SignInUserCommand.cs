namespace Application.Users.Commands;

/// <summary>Raw email + password. Handlers must not reveal which of the two was wrong.</summary>
public sealed record SignInUserCommand(string Email, string Password);