namespace Application.Users;

/// <summary>
/// Why a hand-rolled dispatcher instead of MediatR: MediatR went dual-licensed in mid-2025, and
/// for a solution with a handful of commands its actual value over an interface and a DI lookup is
/// pipeline behaviours and notification fan-out — neither of which this slice uses. This is the
/// whole thing in one interface; if a need for behaviours appears, Wolverine or a
/// source-generator mediator is the honest upgrade, not a licence conversation mid-project.
/// </summary>
public interface ICommandHandler<in TCommand, TResult>
{
    Task<TResult> Handle(TCommand command, CancellationToken ct = default);
}