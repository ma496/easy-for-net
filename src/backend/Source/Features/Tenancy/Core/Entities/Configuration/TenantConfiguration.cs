namespace Backend.Features.Tenancy.Core.Entities.Configuration;

using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>
/// EF Core entity configuration for <see cref="Tenant"/>, mapping it to the <c>tenancy.Tenants</c>
/// table, storing <see cref="TenantStatus"/> as a string, and enforcing a unique index on the
/// normalized identifier that covers soft-deleted tenants as well, so an identifier freed only by
/// deletion can never be taken again.
/// </summary>
public class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    /// <summary>
    /// Configures the table mapping, schema, the status conversion, and the identifier, name,
    /// status and creation-date indexes used by tenant lookup, search, filtering and sorting.
    /// </summary>
    /// <param name="builder">The builder used to configure the <see cref="Tenant"/> entity type.</param>
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("Tenants", "tenancy");

        builder.Property(x => x.Status)
            .HasConversion<string>();

        // Deliberately unfiltered: the uniqueness comparison has to include soft-deleted tenants,
        // and the explicit database name is load-bearing - ExceptionProcessor reports the offending
        // field as the last underscore-separated segment of the constraint name, so
        // "IX_Tenants_Identifier" yields "identifier", the request field name, where the EF default
        // "IX_Tenants_IdentifierNormalized" would yield a name no form knows.
        builder.HasIndex(x => x.IdentifierNormalized)
            .IsUnique()
            .HasDatabaseName("IX_Tenants_Identifier");

        // EF would name this index IX_Tenants_Identifier as well - the name it derives from the
        // column - and two indexes on one table cannot share a name, so the raw-column index is named
        // explicitly and the unique index above keeps the name ExceptionProcessor depends on.
        builder.HasIndex(x => x.Identifier)
            .IsUnique(false)
            .HasDatabaseName("IX_Tenants_IdentifierRaw");
        builder.HasIndex(x => x.Name)
            .IsUnique(false);
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.CreatedAt);

        // Restrict rather than SetNull: an edition is soft-deleted, so EF never issues a real DELETE
        // here, and a physical one done by hand should be refused rather than quietly moving every
        // tenant on that plan onto no plan at all. EditionDeleteEndpoint refuses first, with a
        // message, so nobody meets this constraint in normal use.
        builder.HasOne(x => x.Edition)
            .WithMany()
            .HasForeignKey(x => x.EditionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.EditionId);
    }
}
