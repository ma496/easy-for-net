namespace Backend.Tests.Architect;

using Backend.ShareData.Entities;
using Backend.ShareData.Entities.Base;
using Backend.Features.Identity.Core.Entities;
using Backend.Features.Notifications.Core.Entities;
using Backend.Features.Tenancy.Core.Entities;
using Microsoft.EntityFrameworkCore.Metadata;

/// <summary>
/// Architectural check that a persisted kind cannot be added to the model without being subject to
/// tenant restriction. Every entity type must either implement <see cref="IMayHaveTenant"/> or
/// <see cref="IHaveTenant"/> - which is what makes <see cref="AppDbContext"/> register the named
/// tenant query filter for it - or appear in <see cref="_exemptEntities"/> with a written reason. Because the exemption list stores reasons
/// rather than bare types, exempting a new kind is a documented decision a reviewer can weigh instead
/// of a one-word edit nobody notices.
/// </summary>
public class TenantScopingTests(App app) : AppTestsBase(app)
{
    /// <summary>
    /// Key that <see cref="AppDbContext"/> registers the tenant query filter under. The filter is
    /// checked by name because naming it is what lets a query relax tenant restriction alone while
    /// soft delete stays in force; an unnamed or differently named filter would not give that.
    /// </summary>
    private const string TenantFilterKey = "Tenant";

    /// <summary>
    /// The entity types that deliberately carry no tenant of their own, each with the reason it does
    /// not. Adding an entry here is the only sanctioned way to introduce an unscoped persisted kind,
    /// and the reason is part of the entry so the decision stays on the record.
    /// </summary>
    private static readonly Dictionary<Type, string> _exemptEntities = new()
    {
        [typeof(Tenant)] =
            "Is the scope itself.",
        [typeof(User)] =
            "A global account: one identity across every tenant (AC-048). Its tenant reach is the membership row, not a column here.",
        [typeof(UserRole)] =
            "Tenant derived through Role.TenantId; a column here would duplicate that fact and admit rows where the two disagree (D13).",
        [typeof(Permission)] =
            "Global, code-declared catalogue, identical for every tenant and reconciled from code on every start (AC-040).",
        [typeof(RolePermission)] =
            "Tenant derived through Role.TenantId.",
        [typeof(Token)] =
            "Email-verification and password-reset tokens back account self-service flows that must work with no tenant (AC-051).",
        [typeof(NotificationVisit)] =
            "Tenant derived through Notification.TenantId."
    };

    /// <summary>
    /// Verifies that every tenant-scoped entity type carries the named tenant query filter, so that
    /// a kind becomes tenant-restricted by implementing one of the two markers alone and no
    /// registration has to be remembered when one is added.
    /// </summary>
    [Fact]
    public void Every_Tenant_Scoped_Entity_Carries_The_Tenant_Filter()
    {
        var scopedEntities = PersistedEntities()
            .Where(e => IsTenantScoped(e.ClrType))
            .ToList();

        scopedEntities.Should().NotBeEmpty(
            "the model must contain tenant-scoped entities, otherwise this check passes vacuously");

        var unfiltered = scopedEntities
            .Where(e => e.FindDeclaredQueryFilter(TenantFilterKey) is null)
            .Select(e => e.ClrType.Name)
            .ToList();

        unfiltered.Should().BeEmpty(
            "every tenant-scoped entity must carry the query filter named " + TenantFilterKey +
            ", but these do not: " + string.Join(", ", unfiltered));
    }

    /// <summary>
    /// Verifies that every persisted entity type is either tenant-scoped or exempt for a written
    /// reason. This is the check that fails when a new persisted kind is added and left unscoped:
    /// the only way past it is to make the kind <see cref="IMayHaveTenant"/> or <see cref="IHaveTenant"/>,
    /// or to record why it is neither.
    /// </summary>
    [Fact]
    public void Every_Entity_Is_Scoped_Or_Exempt_With_A_Reason()
    {
        var persistedEntities = PersistedEntities().ToList();

        var unaccounted = persistedEntities
            .Where(e => !IsTenantScoped(e.ClrType) && !_exemptEntities.ContainsKey(e.ClrType))
            .Select(e => e.ClrType.Name)
            .ToList();

        unaccounted.Should().BeEmpty(
            "a persisted entity must either implement IMayHaveTenant or IHaveTenant, or be exempted with a written reason, but these are neither: " +
            string.Join(", ", unaccounted));

        var unreasoned = _exemptEntities
            .Where(exemption => string.IsNullOrWhiteSpace(exemption.Value))
            .Select(exemption => exemption.Key.Name)
            .ToList();

        unreasoned.Should().BeEmpty(
            "an exemption is only sanctioned when it states why the kind carries no tenant, but these state nothing: " +
            string.Join(", ", unreasoned));

        var contradictory = _exemptEntities.Keys
            .Where(IsTenantScoped)
            .Select(type => type.Name)
            .ToList();

        contradictory.Should().BeEmpty(
            "an entity cannot be both tenant-scoped and exempt from tenant restriction, but these claim both: " +
            string.Join(", ", contradictory));

        var stale = _exemptEntities.Keys
            .Where(type => persistedEntities.TrueForAll(e => e.ClrType != type))
            .Select(type => type.Name)
            .ToList();

        stale.Should().BeEmpty(
            "an exemption must name an entity the model still persists, otherwise it is a reason nobody can check, but these are no longer in the model: " +
            string.Join(", ", stale));
    }

    /// <summary>
    /// The entity types the model persists as tables in their own right. Owned types are left out
    /// because their rows live in the owner's table and are restricted with the owner rather than on
    /// their own account.
    /// </summary>
    private IEnumerable<IEntityType> PersistedEntities()
        => DbContext.Model.GetEntityTypes().Where(e => !e.IsOwned());

    /// <summary>
    /// Whether an entity type carries a tenant of its own, by either marker - the same test
    /// <see cref="AppDbContext"/> applies when it registers the filter.
    /// </summary>
    /// <param name="clrType">The entity type to test.</param>
    /// <returns><see langword="true"/> when the type is tenant-scoped.</returns>
    private static bool IsTenantScoped(Type clrType)
        => typeof(IMayHaveTenant).IsAssignableFrom(clrType) || typeof(IHaveTenant).IsAssignableFrom(clrType);
}
