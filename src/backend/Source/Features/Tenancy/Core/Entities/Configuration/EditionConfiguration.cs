namespace Backend.Features.Tenancy.Core.Entities.Configuration;

using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>
/// EF Core entity configuration for <see cref="Edition"/>, mapping it to the <c>tenancy.Editions</c>
/// table and enforcing a unique index on the normalized name that covers soft-deleted editions as
/// well, so a plan name freed only by deletion can never be taken again.
/// </summary>
public class EditionConfiguration : IEntityTypeConfiguration<Edition>
{
    /// <summary>
    /// Configures the table mapping, schema, column lengths and the name and ordering indexes used by
    /// edition lookup, search and sorting.
    /// </summary>
    /// <param name="builder">The builder used to configure the <see cref="Edition"/> entity type.</param>
    public void Configure(EntityTypeBuilder<Edition> builder)
    {
        builder.ToTable("Editions", "tenancy");

        builder.Property(x => x.Name)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(x => x.NameNormalized)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(x => x.Description)
            .HasMaxLength(512);

        // Deliberately unfiltered, and deliberately named: the uniqueness comparison has to include
        // soft-deleted editions, and ExceptionProcessor reports the offending field as the last
        // underscore-separated segment of the constraint name, so "IX_Editions_Name" yields "name" -
        // the request field name - where the EF default "IX_Editions_NameNormalized" would yield a
        // name no form knows.
        builder.HasIndex(x => x.NameNormalized)
            .IsUnique()
            .HasDatabaseName("IX_Editions_Name");

        builder.HasIndex(x => x.DisplayOrder);
        builder.HasIndex(x => x.CreatedAt);
    }
}
