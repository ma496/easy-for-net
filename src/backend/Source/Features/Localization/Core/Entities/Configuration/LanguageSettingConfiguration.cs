namespace Backend.Features.Localization.Core.Entities.Configuration;

using Backend.Features.Localization.Core.Entities;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>
/// EF Core entity configuration for <see cref="LanguageSetting"/>, mapping it to the
/// <c>localization.LanguageSettings</c> table and enforcing at most one row per scope.
/// </summary>
public class LanguageSettingConfiguration : IEntityTypeConfiguration<LanguageSetting>
{
    /// <summary>
    /// Configures the table mapping, schema, column lengths, and the unique per-scope index.
    /// </summary>
    /// <param name="builder">The builder used to configure the <see cref="LanguageSetting"/> entity type.</param>
    public void Configure(EntityTypeBuilder<LanguageSetting> builder)
    {
        builder.ToTable("LanguageSettings", "localization");

        // Maps cleanly to a PostgreSQL "text[]" column via Npgsql - the provider maps a CLR
        // List<string>/string[] property to an array column natively, with no value converter needed.
        builder.Property(x => x.EnabledCultures)
            .IsRequired();

        builder.Property(x => x.DefaultCulture)
            .HasMaxLength(16);

        // At most one row per scope: a tenant's own settings, or the platform's when TenantId is null.
        // Nulls are compared as equal (PostgreSQL 15+ NULLS NOT DISTINCT) so the platform row is unique
        // among itself too, exactly as the tenant rows are among each other.
        builder.HasIndex(x => x.TenantId)
            .IsUnique()
            .AreNullsDistinct(false)
            .HasDatabaseName("IX_LanguageSettings_TenantId");
    }
}
