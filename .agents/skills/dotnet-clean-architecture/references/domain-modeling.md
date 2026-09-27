# Domain modeling

Running example throughout: a restaurant **Reservation** (a table + optional pre-ordered food items). The same shape applies to a Cart, a Subscription, an approval workflow, a support ticket — anything with an identity and a lifecycle.

Contents: [Smart constructors](#smart-constructors) · [Value objects](#value-objects) · [Entities & identity](#entities--identity) · [Status as type](#status-as-type) · [Exhaustiveness](#exhaustiveness) · [Transitions](#transitions) · [Domain events](#domain-events)

## Smart constructors

C# has no runtime schema-validation library built in (nothing like Zod). The equivalent guarantee — "if you hold an instance, it's valid" — comes from a **private constructor + a static `Create` factory** returning `Result<T, Error>`.

```csharp
public readonly record struct PartySize
{
    public int Value { get; }
    private PartySize(int value) => Value = value;

    public static Result<PartySize, DomainError> Create(int value) =>
        value is < 1 or > 20
            ? Result<PartySize, DomainError>.Fail(new InvalidPartySizeError(value))
            : Result<PartySize, DomainError>.Ok(new PartySize(value));

    // Only for callers that already hold trusted data (e.g. straight out of EF Core).
    // Never call this on raw input from an API request — that defeats the whole point.
    public static PartySize FromTrustedSource(int value) => new(value);
}
```

Why this matters: the constructor being private means there is exactly one way to end up with a `PartySize`, and that way already checked the range. Nothing downstream re-validates it — a re-check would imply the type's own guarantee can't be trusted, which is a sign something upstream bypassed `Create`.

Use this for anything that has a validity rule attached to a primitive: an id, a quantity, a date range, an email, a money amount.

## Value objects

A `record` gives structural equality (two instances with the same field values are equal) and `init`-only properties give post-construction immutability. Combined with a `Create` factory, that's the "always valid" guarantee.

```csharp
public readonly record struct Money
{
    public decimal Amount { get; }
    public Currency Currency { get; }
    private Money(decimal amount, Currency currency) { Amount = amount; Currency = currency; }

    public static Result<Money, DomainError> Create(decimal amount, Currency currency) =>
        amount < 0
            ? Result<Money, DomainError>.Fail(new NegativeAmountError(amount))
            : Result<Money, DomainError>.Ok(new Money(amount, currency));
}

public sealed record ReservationItem
{
    public required MenuItemId MenuItemId { get; init; }
    public required int Quantity { get; init; }
    public required Money UnitPrice { get; init; }
}
```

A value object with no validity rule at all doesn't need the `Create` ceremony — a plain `record` with `required init` properties is enough. Reach for the factory only when there's an actual rule to enforce.

## Entities & identity

An entity is defined by a stable id that survives changes to everything else about it, not by its field values.

```csharp
public readonly record struct ReservationId
{
    public Guid Value { get; }
    private ReservationId(Guid value) => Value = value;

    public static ReservationId New() => new(Guid.NewGuid());
    public static Result<ReservationId, DomainError> Create(Guid value) =>
        value == Guid.Empty
            ? Result<ReservationId, DomainError>.Fail(new EmptyIdError(nameof(ReservationId)))
            : Result<ReservationId, DomainError>.Ok(new ReservationId(value));

    public static ReservationId FromTrustedSource(Guid value) => new(value);
}
```

Two different id types that both wrap a `Guid` (`ReservationId`, `CustomerId`) are *not* interchangeable to the compiler, even though they're the same shape at runtime. A method signature `Seat(TableId tableId)` cannot accidentally receive a `ReservationId` — something a raw `Guid` parameter would allow silently.

## Status as type

C# has no built-in discriminated union (as of C# 14). The substitute: an `abstract record` for the shared shape, and one `sealed record` per lifecycle stage, each carrying only the fields valid at that stage.

```csharp
public abstract record ReservationState
{
    public required ReservationId Id { get; init; }
    public required RestaurantId RestaurantId { get; init; }
    public required PartySize PartySize { get; init; }
    public required DateTime RequestedAt { get; init; }
}

public sealed record PendingReservation : ReservationState;

public sealed record ConfirmedReservation : ReservationState
{ public required TableId TableId { get; init; } public required DateTime ConfirmedAt { get; init; } }

public sealed record SeatedReservation : ReservationState
{ public required TableId TableId { get; init; } public required DateTime ConfirmedAt { get; init; } public required DateTime SeatedAt { get; init; } }

public sealed record CompletedReservation : ReservationState
{ public required TableId TableId { get; init; } public required DateTime SeatedAt { get; init; } public required DateTime CompletedAt { get; init; } }

public sealed record CancelledReservation : ReservationState
{ public required DateTime CancelledAt { get; init; } public required string Reason { get; init; } }
```

`CancelledReservation` has no `TableId`. Not "null `TableId`" — the property doesn't exist on the type. Accessing `.TableId` on a `CancelledReservation` is a compile error, not a null check someone forgot to write.

Do **not** model this as one class with a `ReservationStatus` enum plus a pile of nullable fields for "whichever ones apply right now" — that shape (fine at the persistence layer, see `architecture-layers.md`) is exactly what this pattern replaces in the domain.

## Exhaustiveness

Because the base is `abstract` and every case is `sealed`, a `switch` expression over `ReservationState` that misses a case produces compiler warning **CS8509**. Elevate it to a build error so a new lifecycle stage can never silently skip a handler:

```xml
<WarningsAsErrors>CS8509;CS8524</WarningsAsErrors>
```

This is the closest C# gets to Rust's exhaustive `match` — real, but opt-in. Honest limitation: C# has no move semantics, so nothing stops code from holding on to a stale `PendingReservation` reference after a transition "consumed" it. Immutability here means `init`-only properties plus the discipline that transitions always return a new record — not a compiler-enforced guarantee the way Rust's ownership model is.

## Transitions

A transition is a static method whose parameter type is the *specific* predecessor state, never the abstract base. That's what forces every caller to have already narrowed the type — the compiler rejects an invalid call, instead of a runtime `if (state != "Pending") throw`.

```csharp
public static class ReservationTransitions
{
    public static Result<ConfirmedReservation, DomainError> Confirm(PendingReservation r, TableId tableId) =>
        Result<ConfirmedReservation, DomainError>.Ok(new ConfirmedReservation
        {
            Id = r.Id, RestaurantId = r.RestaurantId, PartySize = r.PartySize, RequestedAt = r.RequestedAt,
            TableId = tableId, ConfirmedAt = DateTime.UtcNow,
        });

    public static SeatedReservation Seat(ConfirmedReservation r) => new()
    {
        Id = r.Id, RestaurantId = r.RestaurantId, PartySize = r.PartySize, RequestedAt = r.RequestedAt,
        TableId = r.TableId, ConfirmedAt = r.ConfirmedAt, SeatedAt = DateTime.UtcNow,
    };

    // Cancellation is allowed from Pending OR Confirmed — two overloads, not one method
    // taking the abstract ReservationState. Each overload only exists where the business
    // actually allows the transition (you cannot cancel a SeatedReservation this way).
    public static CancelledReservation Cancel(PendingReservation r, string reason) => new()
    { Id = r.Id, RestaurantId = r.RestaurantId, PartySize = r.PartySize, RequestedAt = r.RequestedAt,
      CancelledAt = DateTime.UtcNow, Reason = reason };

    public static CancelledReservation Cancel(ConfirmedReservation r, string reason) => new()
    { Id = r.Id, RestaurantId = r.RestaurantId, PartySize = r.PartySize, RequestedAt = r.RequestedAt,
      CancelledAt = DateTime.UtcNow, Reason = reason };
}
```

No unit test is needed to prove "you can't seat a reservation that was never confirmed" — there is no `Seat(PendingReservation)` overload, so that call does not compile. Save unit tests for rules the type system genuinely can't express (e.g. "a party of more than 8 needs manager approval to confirm").

## Domain events

A transition can optionally return `(ReservationState NewState, ReservationDomainEvent Event)` so the application handler dispatches the event only after a successful save — still zero business logic in the handler, just relaying a decision the domain already made.

```csharp
public abstract record ReservationDomainEvent(ReservationId ReservationId, DateTime OccurredAt);
public sealed record ReservationConfirmed(ReservationId ReservationId, TableId TableId, DateTime OccurredAt) : ReservationDomainEvent(ReservationId, OccurredAt);
public sealed record ReservationCancelled(ReservationId ReservationId, string Reason, DateTime OccurredAt) : ReservationDomainEvent(ReservationId, OccurredAt);
```

If you're not sure what events exist for a domain yet, don't guess — event-storm it first: write every past-tense thing that happens on a sticky note, order them left to right, and only then decide which ones are aggregate boundaries versus policies versus read models. A rule invented without going back to that list ("let's just add a `Paused` status") is usually either a missing event the domain experts forgot to mention, or UI state that never belonged in the domain at all.
