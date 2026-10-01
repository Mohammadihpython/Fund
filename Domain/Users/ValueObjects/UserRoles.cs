using System.Collections.Immutable;
using SharedKernel;

namespace Domain.Users;

/// <summary>
/// The set of roles an account holds, as a value object rather than a bare
/// <see cref="ImmutableArray{T}"/> field.
/// <para>
/// Why this exists: <c>ImmutableArray&lt;T&gt;</c> does not implement structural equality — two
/// arrays holding the same elements in the same order are <em>not</em> equal, because
/// <see cref="ImmutableArray{T}"/> inherits <c>Equals</c> from its array reference. A
/// <c>record</c> therefore treats <c>new UserState(roles: [a, b])</c> and
/// <c>new UserState(roles: [a, b])</c> as different objects, and anything that relied on record
/// equality — a unit test asserting a mapped round-trip, an event dedup, a cache key — silently
/// stops working. Wrapping the collection in a record that compares with
/// <see cref="SetEquals(Set, Set, IEqualityComparer{Role})"/> restores the structural equality
/// the domain expects, and gives the set one obvious home for "add a role", "remove a role" and
/// "count".
/// </para>
/// <para>
/// Roles are a set, not a list: the order two roles were granted in is not a fact anyone should
/// be able to observe or compare.
/// </para>
/// </summary>
public sealed record UserRoles
{
    private readonly ImmutableHashSet<Role> _roles;

    private UserRoles(ImmutableHashSet<Role> roles) => _roles = roles;

    public static UserRoles Empty { get; } = new(ImmutableHashSet<Role>.Empty);

    public static UserRoles Create(IEnumerable<Role>? roles) =>
        roles is null ? Empty : new UserRoles(roles.ToImmutableHashSet());

    public static UserRoles FromTrustedSource(IEnumerable<Role>? roles) => Create(roles);

    public int Count => _roles.Count;

    public bool Contains(Role role) => _roles.Contains(role);

    public IEnumerable<Role> AsEnumerable() => _roles;

    public UserRoles With(Role role) =>
        _roles.Contains(role) ? this : new UserRoles(_roles.Add(role));

    public UserRoles Without(Role role) =>
        _roles.Contains(role) ? new UserRoles(_roles.Remove(role)) : this;

    public bool Equals(UserRoles? other) =>
        other is not null && _roles.SetEquals(other._roles);

    public override int GetHashCode()
    {
        // Order-independent so it agrees with Equals, which is set-based.
        var hash = default(HashCode);
        foreach (var role in _roles)
            hash.Add(role);
        return hash.ToHashCode();
    }

    public override string ToString() => $"[{string.Join(", ", _roles.Select(r => r.Value))}]";
}
