namespace Backend.Features.Localization.Core.Entities.Configuration;

using Backend.Features.Localization.Core.Entities;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>
/// EF Core entity configuration for <see cref="LocalizationText"/>, mapping it to the
/// <c>localization.LocalizationTexts</c> table and enforcing at most one override per scope, culture
/// and key.
/// </summary>
public class LocalizationTextConfiguration : IEntityTypeConfiguration<LocalizationText>
{
    /// <summary>
    /// Configures the table mapping, schema, column lengths, and the unique (tenant, culture, key)
    /// index resolution and the write endpoints both rely on.
    /// </summary>
    /// <param name="builder">The builder used to configure the <see cref="LocalizationText"/> entity type.</param>
    public void Configure(EntityTypeBuilder<LocalizationText> builder)
    {
        builder.ToTable("LocalizationTexts", "localization");

        builder.Property(x => x.Culture)
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(x => x.Key)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(x => x.Value)
            .HasMaxLength(4000)
            .IsRequired();

        // At most one override per scope, culture and key. Nulls are compared as equal (PostgreSQL 15+
        // NULLS NOT DISTINCT) so that platform overrides (TenantId null) are unique among themselves
        // too, exactly as a platform-scoped Role name is. The database name ends in "Key" because a
        // violation is reported against the last segment, and "key" is the field PUT /localization/texts
        // names. The leading TenantId column also serves every "overrides of the active scope" read.
        builder.HasIndex(x => new { x.TenantId, x.Culture, x.Key })
            .IsUnique()
            .AreNullsDistinct(false)
            .HasDatabaseName("IX_LocalizationTexts_TenantId_Culture_Key");
    }
}
