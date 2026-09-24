namespace Backend.Tests.Features.Tenancy.Core;

using Backend.Exceptions;
using Backend.Features.Notifications.Core;
using Backend.Features.Tenancy.Core;

/// <summary>
/// Tests for tenant scope outside a user request - the standing of scheduled and queued work
///.
/// </summary>
/// <remarks>
/// <para>
/// Nothing here goes through <c>Client</c>. Every dependency is resolved from a service scope of
/// its own, the way a job activator resolves a job: there is no HTTP request, so there is no
/// pre-processor to establish a tenant and the scope starts unresolved. That is the state these tests
/// are about - work that runs outside a request has to be told which tenant it acts for, and failing
/// loudly when nobody says is the point rather than an inconvenience.
/// </para>
/// <para>
/// The work arranged is a notification raised for a whole tenant, because notifications are genuinely
/// tenant-scoped (<see cref="Backend.Features.Notifications.Core.Entities.Notification"/> carries the
/// tenant it was raised in) and a direct database statement over them is observable in what it leaves
/// behind: the tenant the job was given loses its rows, and every other tenant keeps its own.
/// </para>
/// </remarks>
public class BackgroundTenantScopeTests(App app) : TenancyTestsBase(app)
{
    /// <summary>
    /// Verifies that a write reached with no scope established fails, and that the very same write
    /// succeeds once a tenant is named explicitly - so that "acts for exactly one tenant" is something
    /// a job states rather than something it inherits.
    /// </summary>
    [Fact]
    public async Task Background_Work_Requires_An_Explicit_Tenant()
    {
        var tenant = await CreateTenantAsync();
        var marker = NewMarker();

        var notifications = Service<INotificationService>();
        var tenantContext = Service<ITenantContext>();

        using (tenantContext.BeginUnscoped())
        {
            var raised = async () => await notifications.NewTenantNotificationAsync(
                NotificationType.Info,
                marker,
                marker);

            await raised.Should().ThrowAsync<TenantScopeNotEstablishedException>();
        }

        using (tenantContext.BeginTenant(tenant.Id))
        {
            await notifications.NewTenantNotificationAsync(
                NotificationType.Info,
                marker,
                marker,
                cancellationToken: TestContext.Current.CancellationToken);
        }

        var stored = await DbContext.Notifications
            .AcrossAllTenants()
            .Where(notification => notification.TitleKey == marker)
            .ToListAsync(TestContext.Current.CancellationToken);

        stored.Should().ContainSingle();
        stored[0].TenantId.Should().Be(tenant.Id);
    }

    /// <summary>
    /// Verifies that a job entry point invoked directly fails when it is given no tenant, and that
    /// given one it processes exactly that tenant's rows - neither every tenant's nor none.
    /// </summary>
    [Fact]
    public async Task Job_Without_A_Tenant_Fails_And_With_One_Processes_Exactly_That_Tenant()
    {
        var tenantA = await CreateTenantAsync();
        var tenantB = await CreateTenantAsync();
        var marker = NewMarker();

        await RaiseTenantNotificationAsync(tenantA.Id, marker);
        await RaiseTenantNotificationAsync(tenantB.Id, marker);

        var job = new TenantNotificationSweepJob(App.Services);

        var invokedWithoutATenant = async () => await job.RunAsync(tenantId: null, marker);
        await invokedWithoutATenant.Should().ThrowAsync<TenantScopeNotEstablishedException>();

        var processed = await job.RunAsync(tenantA.Id, marker);
        processed.Should().Be(1, "the rows of the tenant the job was given, and no others");

        var remaining = await DbContext.Notifications
            .AcrossAllTenants()
            .Where(notification => notification.TitleKey == marker)
            .ToListAsync(TestContext.Current.CancellationToken);

        remaining.Should().ContainSingle();
        remaining[0].TenantId.Should().Be(tenantB.Id, "the other tenant's row was left untouched");
    }

    /// <summary>
    /// A marker no other test can match, so that the rows these tests sweep are only ever their own.
    /// </summary>
    private static string NewMarker() => $"job-{Guid.NewGuid():N}";

    /// <summary>
    /// Raises one tenant-wide notification from a scope of its own, the way a request would - which is
    /// what puts rows in both tenants for the job below to be told which of them to act for.
    /// </summary>
    /// <param name="tenantId">The tenant to raise the notification in.</param>
    /// <param name="marker">The title key marking the notification as this test's.</param>
    private async Task RaiseTenantNotificationAsync(Guid tenantId, string marker)
    {
        using var scope = App.Services.CreateScope();
        var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        var notifications = scope.ServiceProvider.GetRequiredService<INotificationService>();

        using var tenantScope = tenantContext.BeginTenant(tenantId);
        await notifications.NewTenantNotificationAsync(NotificationType.Info, marker, marker);
    }

    /// <summary>
    /// A recurring-job entry point in the shape a scheduled job is activated in: no HTTP request, its
    /// dependencies resolved from a scope of its own, and the tenant it acts for supplied by whoever
    /// scheduled the run rather than inferred from a request. It performs the same kind of direct
    /// database statement the application's own maintenance jobs perform, over a genuinely
    /// tenant-scoped table, so "processed exactly one tenant" is visible in the rows it leaves behind.
    /// </summary>
    /// <param name="services">The root provider a scope is created from, standing in for the job activator.</param>
    private sealed class TenantNotificationSweepJob(IServiceProvider services)
    {
        /// <summary>
        /// Runs the job for one tenant, or with no tenant at all when none is given - which the kernel
        /// is expected to refuse rather than read as "every tenant".
        /// </summary>
        /// <param name="tenantId">The tenant to act for, or <see langword="null"/> to run with none.</param>
        /// <param name="marker">The title key identifying the rows this run is to act on.</param>
        /// <returns>The number of rows the run removed.</returns>
        public async Task<int> RunAsync(Guid? tenantId, string marker)
        {
            using var scope = services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();

            using var tenantScope = tenantId is { } activeTenantId
                ? tenantContext.BeginTenant(activeTenantId)
                : null;

            return await dbContext.Notifications
                .Where(notification => notification.TitleKey == marker)
                .ExecuteDeleteAsync(TestContext.Current.CancellationToken);
        }
    }
}
