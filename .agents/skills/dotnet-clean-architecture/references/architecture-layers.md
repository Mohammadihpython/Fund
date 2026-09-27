# Architecture layers

Contents: [Layout](#layout) · [The dependency rule, enforced](#the-dependency-rule-enforced) · [Repository: port in Domain, adapter in Infrastructure](#repository-port-in-domain-adapter-in-infrastructure) · [The persistence-mapping seam](#the-persistence-mapping-seam) · [Handlers: four steps, nothing else](#handlers-four-steps-nothing-else) · [Dispatching commands without MediatR](#dispatching-commands-without-mediatr)

## Layout

Organize by feature first, layer second (**Vertical Slice**), where each slice is internally a small **Clean/Onion Architecture**:

```
Reservation.Domain.csproj          → references: (nothing internal)
Reservation.Application.csproj     → references: Reservation.Domain
Reservation.Infrastructure.csproj  → references: Reservation.Domain, Reservation.Application
Reservation.Api.csproj             → references: Reservation.Application, Reservation.Infrastructure   // composition root
```

- **Domain** — entities, value objects, state-machine records, transition methods, domain events, repository *interfaces* (ports). No dependency on anything else in the solution.
- **Application** — command/query handlers. Orchestration only: validate, fetch, call the domain, persist.
- **Infrastructure** — EF Core entities + `DbContext`, repository *implementations* (adapters), external service clients, background workers for policies.
- **Api** — controllers or minimal-API endpoint definitions, request/response DTOs, composition root (`Program.cs` wires everything together).

## The dependency rule, enforced

Wobbly enforcement ("we agreed not to do that in code review") doesn't survive a deadline. Two things make it real:

1. **The `.csproj` graph itself.** If `Reservation.Domain.csproj` cannot compile without a `<ProjectReference>` to Infrastructure, the rule was already broken — and that's a build failure, not a review comment.
2. **An automated architecture test**, run in CI on every PR:

```csharp
[Fact]
public void Domain_Should_Not_Depend_On_Infrastructure_Or_Application()
{
    var result = Types.InAssembly(typeof(ReservationState).Assembly)
        .ShouldNot()
        .HaveDependencyOnAny("Reservation.Infrastructure", "Reservation.Application")
        .GetResult();

    result.IsSuccessful.Should().BeTrue(result.FailingTypeNames is { Count: > 0 }
        ? $"Leaked into: {string.Join(", ", result.FailingTypeNames)}" : "");
}
```

(`NetArchTest.Rules` is the common choice for this; `ArchUnitNET` is the alternative if you want richer rule composition. Either is a small, low-risk dependency — check it's still actively maintained before pinning a version, the way you would for any library.)

## Repository: port in Domain, adapter in Infrastructure

```csharp
// Domain — port, zero implementation
public interface IReservationRepository
{
    Task<ReservationState?> FindByIdAsync(ReservationId id, CancellationToken ct = default);
    Task SaveAsync(ReservationState reservation, CancellationToken ct = default);
}
```

```csharp
// Infrastructure — adapter
public sealed class EfReservationRepository(ReservationDbContext db) : IReservationRepository
{
    public async Task<ReservationState?> FindByIdAsync(ReservationId id, CancellationToken ct = default)
    {
        var raw = await db.Reservations.FirstOrDefaultAsync(r => r.Id == id.Value, ct);
        return raw is null ? null : ReservationMapper.FromPersistence(raw);
    }

    public async Task SaveAsync(ReservationState reservation, CancellationToken ct = default)
    {
        var existing = await db.Reservations.FirstOrDefaultAsync(r => r.Id == reservation.Id.Value, ct)
                       ?? new ReservationEntity();
        ReservationMapper.ApplyToEntity(reservation, existing);
        if (existing.Id == default) db.Reservations.Add(existing);
        await db.SaveChangesAsync(ct);
    }
}
```

(The constructor above is a C# 12+ **primary constructor** — `db` is captured directly as a private field with no hand-written `private readonly` line. See `dotnet10-idioms.md` for when this is and isn't a readability win.)

Notice what's absent: no `switch` on status, no business rule, anywhere in the adapter. Every decision already happened in the domain; the adapter just moves bytes. If you can swap EF Core/SQL Server for Dapper/Postgres by editing only Infrastructure, the port is doing its job. If Application or Domain also need edits, something leaked through the interface.

## The persistence-mapping seam

The one sanctioned exception to "Domain knows nothing about the outside": a single mapper file is allowed to know about the EF Core entity shape, because deciding *which fields are valid for which status* is itself a domain rule, not a database concern.

```csharp
// Infrastructure — the entity has one status column and every optional field nullable.
// That shape is correct *here* — a database row is inherently "one shape, some columns unused".
public class ReservationEntity
{
    public Guid Id { get; set; }
    public ReservationStatus Status { get; set; }   // enum — lives only in this file's neighborhood
    public Guid? TableId { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public DateTime? SeatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancelReason { get; set; }
}
```

```csharp
// Domain/ReservationMapper.cs — the ONLY file in Domain allowed to reference ReservationEntity.
public static class ReservationMapper
{
    public static ReservationState FromPersistence(ReservationEntity raw) => raw.Status switch
    {
        ReservationStatus.Pending   => new PendingReservation { /* ... */ },
        ReservationStatus.Confirmed => new ConfirmedReservation { TableId = TableId.FromTrustedSource(raw.TableId!.Value), ConfirmedAt = raw.ConfirmedAt!.Value, /* ... */ },
        ReservationStatus.Seated    => new SeatedReservation { /* ... */ },
        ReservationStatus.Completed => new CompletedReservation { /* ... */ },
        ReservationStatus.Cancelled => new CancelledReservation { CancelledAt = raw.CancelledAt!.Value, Reason = raw.CancelReason!, /* ... */ },
        _ => throw new UnknownReservationStatusException(raw.Status),   // guarded by CS8509-as-error above
    };

    public static void ApplyToEntity(ReservationState state, ReservationEntity target) { /* mirror image */ }
}
```

Keep this to **one** mapper pair (`FromPersistence`/`ApplyToEntity`) per aggregate. A second, ad-hoc `raw.Status switch` anywhere else in the codebase should be merged into this one — that's the check worth adding to a PR template or the architecture-test suite.

## Handlers: four steps, nothing else

```csharp
public sealed class ConfirmReservationHandler(IReservationRepository repo)
{
    public async Task<Result<ConfirmedReservation, DomainError>> Handle(ConfirmReservationCommand cmd, CancellationToken ct = default)
    {
        var idResult = ReservationId.Create(cmd.ReservationId);                          // 1. validate
        if (!idResult.IsSuccess) return Result<ConfirmedReservation, DomainError>.Fail(idResult.Error);

        var reservation = await repo.FindByIdAsync(idResult.Value, ct);                  // 2. fetch
        if (reservation is null) return Result<ConfirmedReservation, DomainError>.Fail(new NotFoundError(cmd.ReservationId));
        if (reservation is not PendingReservation pending)
            return Result<ConfirmedReservation, DomainError>.Fail(new InvalidStateError(reservation, expected: "Pending"));

        var result = ReservationTransitions.Confirm(pending, TableId.FromTrustedSource(cmd.TableId));  // 3. domain
        if (!result.IsSuccess) return result;

        await repo.SaveAsync(result.Value, ct);                                          // 4. persist
        return result;
    }
}
```

Litmus test: delete every line that isn't validate/fetch/call-domain/persist. If the file shrank, logic had leaked — move it into a transition method on the domain type.

## Dispatching commands without MediatR

MediatR moved to a commercial license in mid-2025 (free tier for individuals and companies under roughly $5M revenue; paid above that — details drift, verify current terms before deciding). For .NET 10, a hand-rolled dispatcher is a small, fully-owned alternative and avoids the licensing question outright:

```csharp
public interface ICommandHandler<in TCommand, TResult>
{
    Task<TResult> Handle(TCommand command, CancellationToken ct = default);
}

public sealed class Dispatcher(IServiceProvider services)
{
    public Task<TResult> Send<TCommand, TResult>(TCommand command, CancellationToken ct = default)
        => services.GetRequiredService<ICommandHandler<TCommand, TResult>>().Handle(command, ct);
}
```

Register handlers with `AddScoped<ICommandHandler<ConfirmReservationCommand, Result<ConfirmedReservation, DomainError>>, ConfirmReservationHandler>()` (or a one-line assembly scan). This has no pipeline behaviors, retries, or notification/fan-out support out of the box — if you need those, that's a real reason to pull in a library (Wolverine, or a source-generator-based mediator) rather than a reason to default to MediatR out of habit. See `trade-offs.md#mediatr` for the fuller decision.
