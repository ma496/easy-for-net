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

        // Deliberately unfiltered: the uniqueness comparison has to include soft-deleted tenants.
        // ExceptionProcessor reports the offending field as the last underscore-separated segment of
        // the constraint name with "Normalized" dropped, so both indexes yield the "identifier"
        // request field.
        builder.HasIndex(x => x.IdentifierNormalized)
            .IsUnique()
            .HasDatabaseName("IX_Tenants_IdentifierNormalized");
        builder.HasIndex(x => x.Identifier)
            .IsUnique()
            .HasDatabaseName("IX_Tenants_Identifier");
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
