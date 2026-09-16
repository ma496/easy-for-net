namespace Backend.Data.Entities.Configuration;

using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>
/// EF Core entity configuration for <see cref="TenantMembership"/>, mapping it to the
/// <c>tenancy.TenantMemberships</c> table, requiring the tenant attribution, enforcing one live
/// membership per tenant and user, and mapping PostgreSQL's <c>xmin</c> system column as the row's
/// concurrency token.
/// </summary>
public class TenantMembershipConfiguration : IEntityTypeConfiguration<TenantMembership>
{
    /// <summary>
    /// Configures the table mapping, the required tenant attribution, the concurrency token, and the
    /// indexes that serve the duplicate check, the member list, and the tenants-of-this-user query.
    /// </summary>
    /// <param name="builder">The builder used to configure the <see cref="TenantMembership"/> entity type.</param>
    public void Configure(EntityTypeBuilder<TenantMembership> builder)
    {
        builder.ToTable("TenantMemberships", "tenancy");

        // For every other tenant-scoped entity a null TenantId means platform scope, but a membership
        // without a tenant is meaningless, so the column is required here rather than in the entity.
        builder.Property(x => x.TenantId)
            .IsRequired();

        // PostgreSQL's xmin system column as a shadow concurrency token: two concurrent replacements
        // of the same member's role assignments both touch this row, so the losing writer fails with
        // DbUpdateConcurrencyException instead of silently overwriting a set it never saw. The value
        // is read in tests with dbContext.Entry(membership).Property("xmin").
        //
        // Written out rather than as UseXminAsConcurrencyToken(): that extension method no longer
        // exists in Npgsql.EntityFrameworkCore.PostgreSQL 10, and these three calls are exactly what
        // it used to expand to. A uint property that is a concurrency token generated on add and on
        // update is what NpgsqlPostgresModelFinalizingConvention maps onto the xmin system column,
        // and NpgsqlMigrationsSqlGenerator keeps system columns out of the generated migration, so
        // no AddColumn is emitted for it. Do not "restore" the old call - it will not compile.
        builder.Property<uint>("xmin")
            .HasColumnName("xmin")
            .IsRowVersion();

        // One live membership per tenant and user. The filter keeps removed memberships out of the
        // comparison, so a previously removed member can be added again and an old removed row never
        // makes the user "already a member". The endpoint reports the duplicate first; this index is
        // the race backstop. The name ends in "User" so a violation is reported on the "user" field.
        builder.HasIndex(x => new { x.TenantId, x.UserId })
            .IsUnique()
            .HasFilter("\"IsDeleted\" = false")
            .HasDatabaseName("IX_TenantMemberships_TenantId_User");

        // "Which tenants does this account belong to" - asked at sign-in, on every tenant switch, and
        // by the session middleware on every request.
        builder.HasIndex(x => x.UserId);

        // Default sort of the paged membership list.
        builder.HasIndex(x => x.CreatedAt);
    }
}