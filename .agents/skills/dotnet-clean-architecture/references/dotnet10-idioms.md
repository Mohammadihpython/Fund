# .NET 10 / C# 14 idioms

.NET 10 shipped November 2025 as an LTS release (supported to November 2028) with C# 14. None of this changes the architecture in the other reference files — it changes how tersely and safely you can write the domain/value-object code that architecture calls for. Reach for these; don't write the old workaround out of habit.

Contents: [field keyword](#field-keyword) · [Extension members](#extension-members) · [Null-conditional assignment](#null-conditional-assignment) · [Primary constructors](#primary-constructors) · [Required members](#required-members) · [Collection expressions & params collections](#collection-expressions--params-collections) · [File-based apps for quick checks](#file-based-apps-for-quick-checks) · [What actually changed in the libraries](#what-actually-changed-in-the-libraries)

## field keyword

Write validation or normalization inside a property accessor without declaring a backing field by hand — `field` refers to the compiler-synthesized one.

```csharp
public sealed class MenuItemDraft
{
    public required string Name
    {
        get;
        set => field = value.Trim() is { Length: > 0 } t
            ? t
            : throw new ArgumentException("Menu item name cannot be blank.");
    }
}
```

Use this for the small normalization/guard logic that used to justify a hand-written `private string _name;` plus a getter/setter pair. It is *not* a replacement for a smart constructor on a value object — a `Create` factory returning `Result<T, Error>` is still the right shape whenever failure needs to be handled by the caller rather than thrown.

## Extension members

Extension methods have existed since C# 3. C# 14 adds extension **properties, operators, and static members** via `extension` blocks — useful for adding read-only conveniences to a type you don't own (including your own value objects, if you want to keep the core record minimal).

```csharp
public static class ReservationStateExtensions
{
    extension(ReservationState state)
    {
        public bool IsTerminal => state is CompletedReservation or CancelledReservation;
        public bool IsActive => !state.IsTerminal;
    }
}

// usage reads exactly like a real property:
if (reservation.IsTerminal) { /* ... */ }
```

Good fit: derived, read-only questions about a state ("is this terminal?", "is this overdue?") that don't need to live on the record itself. Don't use it to smuggle business logic that belongs in a transition method — this is a convenience for *reading* the domain, not for changing it.

## Null-conditional assignment

`?.` and `?[]` can now sit on the left-hand side of an assignment or compound assignment.

```csharp
// Before: a null check just to guard an assignment.
if (customer is not null) customer.LoyaltyPoints += 10;

// C# 14:
customer?.LoyaltyPoints += 10;
```

The right-hand side isn't evaluated at all if the left side is null — this isn't just shorter, it also avoids calling something like `ComputeBonusPoints()` for no reason. `++`/`--` are not supported this way; only assignment and compound assignment (`+=`, `-=`, etc.).

## Primary constructors

(C# 12, but still under-used in older Clean Architecture examples you'll find online — worth calling out explicitly.) For a class whose constructor only assigns parameters to fields, skip the boilerplate:

```csharp
public sealed class ConfirmReservationHandler(IReservationRepository repo)
{
    public async Task<Result<ConfirmedReservation, DomainError>> Handle(ConfirmReservationCommand cmd, CancellationToken ct = default)
    {
        var reservation = await repo.FindByIdAsync(/* ... */);
        // ...
    }
}
```

Reach for this on handlers, repositories, and other single-dependency infrastructure classes. Don't reach for it on domain entities/value objects — those use the `private constructor + static Create` shape from `domain-modeling.md`, which a primary constructor can't express (a primary constructor's parameters are public-ish by default and don't give you a validation hook before the object exists).

## Required members

(C# 11.) `required` on an `init` property forces every caller to set it, checked at compile time — this is what makes the sealed-record state pattern in `domain-modeling.md` safe: forgetting to set `TableId` on a `ConfirmedReservation` is a compile error, not a runtime null.

## Collection expressions & params collections

```csharp
IReadOnlyList<ReservationItem> items = [];                 // C# 12 collection expression
IReadOnlyList<ReservationItem> combined = [.. existing, newItem];   // spread

// C# 13: params now accepts more than T[] — e.g. a span, for allocation-free call sites.
public static Money Sum(params ReadOnlySpan<Money> amounts) { /* ... */ }
```

Small readability win in the kind of aggregation code (`Items`, totals) that shows up constantly in this style of domain model.

## File-based apps for quick checks

.NET 10 can run a single `.cs` file directly — `dotnet run Scratch.cs` — with no `.csproj`. This is genuinely useful for sanity-checking one transition method or smart constructor in isolation while you're designing a rule, before it earns a place in the real project and a proper unit test. It is not a substitute for the project structure in `architecture-layers.md` — treat it as a scratchpad, not a deployment shape.

## What actually changed in the libraries

Not a language feature, but worth knowing before defaulting to patterns from older tutorials:

- **EF Core 10** adds *named* query filters — you can attach several independent global filters to one entity (e.g. soft-delete and multi-tenancy) and disable them individually with `IgnoreQueryFilters(["FilterName"])`, instead of the old all-or-nothing. It also makes complex-type-mapped-to-JSON-column the recommended way to store structured data on an entity, superseding the older owned-entity-type JSON pattern.
- **xUnit v3** (built on Microsoft.Testing.Platform) is the safe default for new test projects; **TUnit** is the source-generator/Native-AOT-first alternative worth knowing about if test-suite startup time or AOT matters to the project. They use different runners (VSTest vs. Microsoft.Testing.Platform directly) — don't assume a `dotnet test` invocation that mixes both frameworks in one run will behave like a single-framework suite.
- **MediatR and AutoMapper** are dual-licensed as of mid-2025. See `trade-offs.md#mediatr` before adding either as a default dependency.
