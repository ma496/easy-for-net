namespace Backend.Tests.Features.Tenancy.Core;

using Backend.Data.Entities;

/// <summary>
/// Tests for the shape the tenancy schema was built with - the columns and the constraints the rest of
/// the feature is allowed to rely on (AC-001, AC-085).
/// </summary>
/// <remarks>
/// <para>
/// The shape is asserted twice over, because it is declared twice over: once in the model, where every
/// later query is written against it, and once in the database, where a migration has to have produced
/// it. A model that declares a unique index the database does not carry would let two rows collide in
/// production while every test that goes through the model kept passing, so the second half of each
/// assertion is asked of PostgreSQL rather than of EF Core.
/// </para>
/// <para>
/// The identifier constraint is the one whose <em>name</em> is load-bearing rather than merely
/// descriptive: the exception processor reports the offending field as the last underscore-separated
/// segment of the violated constraint's name, so the create surfaces hand the caller a refusal
/// attributed to <c>Identifier</c> only for as long as the index is called
/// <c>IX_Tenants_Identifier</c>. That is why the name is asserted here rather than left to the default
/// EF would derive.
/// </para>
/// </remarks>
public class TenantSchemaTests(App app) : TenancyTestsBase(app)
{
    /// <summary>
    /// The unique index behind the identifier comparison, named explicitly so the refusal a duplicate
    /// identifier earns can be attributed to the field the caller has to change.
    /// </summary>
    private const string IdentifierIndexName = "IX_Tenants_Identifier";

    /// <summary>
    /// The unique index behind a role name being unique within its tenant.
    /// </summary>
    private const string RoleNameIndexName = "IX_Roles_TenantId_Name";

    /// <summary>
    /// Verifies that the model describes a tenant as the tenant lifecycle needs it - the identity it is
    /// addressed by, the state it is in, who created and last changed it, the soft-delete pair that lets
    /// it be retired without being erased, and a unique index over the normalized identifier
    /// (AC-001).
    /// </summary>
    [Fact]
    public void Tenant_Entity_Shape()
    {
        var tenantType = DbContext.Model.FindEntityType(typeof(Tenant));

        tenantType.Should().NotBeNull("the tenancy schema is built from the Tenant entity");

        var propertyNames = tenantType!.GetProperties().Select(property => property.Name).ToList();

        propertyNames.Should().Contain(
            ["Name", "Identifier", "IdentifierNormalized", "Status", "SystemCreated",
             "CreatedAt", "CreatedBy", "UpdatedAt", "UpdatedBy",
             "IsDeleted", "DeletedAt"],
            "the tenant lifecycle is described by these columns and no others are needed to address, "
            + "attribute and retire a tenant");

        var identifierIndexes = tenantType.GetIndexes()
            .Where(index => index.Properties.Select(property => property.Name).SequenceEqual(["IdentifierNormalized"]))
            .ToList();

        identifierIndexes.Should().ContainSingle("the normalized identifier is what every uniqueness comparison runs against");
        identifierIndexes[0].IsUnique.Should().BeTrue("two tenants may not share an identifier");
        identifierIndexes[0].GetDatabaseName().Should().Be(
            IdentifierIndexName,
            "the refusal a duplicate identifier earns is attributed to the field named by the constraint's last segment");
    }

    /// <summary>
    /// Verifies that the schema the suite is running against is complete from a first-time creation:
    /// the model declares nothing the migrations have not applied, the tenancy tables are there, and
    /// both unique indexes exist in the database rather than only in the model (AC-085).
    /// </summary>
    /// <remarks>
    /// The database this runs against was created by the shared fixture migrating an empty one, so an
    /// empty pending-migration set is the statement that a first-time creation reaches the whole
    /// schema - which is what a generated project's own first start has to do.
    /// </remarks>
    [Fact]
    public async Task Schema_Is_Complete_From_A_First_Time_Creation()
    {
        var pendingMigrations = await DbContext.Database
            .GetPendingMigrationsAsync(TestContext.Current.CancellationToken);

        pendingMigrations.Should().BeEmpty(
            "the database was migrated from empty, so nothing the model declares is still waiting to be applied");

        var tenancyTables = await ReadNamesAsync(
            "SELECT tablename FROM pg_tables WHERE schemaname = 'tenancy'");

        tenancyTables.Should().Contain(
            ["Tenants", "TenantMemberships"],
            "the tenant table and the membership that places an account inside one are the two tables this feature adds");

        var identityTables = await ReadNamesAsync(
            "SELECT tablename FROM pg_tables WHERE schemaname = 'identity'");

        identityTables.Should().Contain("Roles", "a role is what carries authority inside a tenant");

        var tenancyIndexes = await ReadNamesAsync(
            $"SELECT indexname FROM pg_indexes WHERE indexname IN ('{IdentifierIndexName}', '{RoleNameIndexName}')");

        tenancyIndexes.Should().BeEquivalentTo(
            [IdentifierIndexName, RoleNameIndexName],
            "both uniqueness comparisons are decided by the database, not only by the code that runs before it");
    }

    /// <summary>
    /// Reads one column of names out of the database through the connection the context itself uses, so
    /// what is asserted is what PostgreSQL holds rather than what EF Core would project from the model.
    /// </summary>
    /// <param name="query">The query to run; it has to project the names as its first column.</param>
    /// <returns>The names the query returned, in the order it returned them.</returns>
    private async Task<List<string>> ReadNamesAsync(string query)
    {
        var connection = DbContext.Database.GetDbConnection();

        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(TestContext.Current.CancellationToken);
        }

        await using var command = connection.CreateCommand();
        command.CommandText = query;

        var names = new List<string>();
        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        while (await reader.ReadAsync(TestContext.Current.CancellationToken))
        {
            names.Add(reader.GetString(0));
        }

        return names;
    }
}