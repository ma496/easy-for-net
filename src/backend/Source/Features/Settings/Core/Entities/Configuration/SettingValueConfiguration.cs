namespace Backend.Features.Settings.Core.Entities.Configuration;

using Backend.Features.Settings.Core.Entities;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>
/// EF Core entity configuration for <see cref="SettingValue"/>, mapping it to the
/// <c>settings.SettingValues</c> table and enforcing at most one row per scope and setting.
/// </summary>
public class SettingValueConfiguration : IEntityTypeConfiguration<SettingValue>
{
    /// <summary>
    /// Configures the table mapping, schema, column types, and the unique (tenant, name) index
    /// resolution and the write endpoints both rely on.
    /// </summary>
    /// <param name="builder">The builder used to configure the <see cref="SettingValue"/> entity type.</param>
    public void Configure(EntityTypeBuilder<SettingValue> builder)
    {
        builder.ToTable("SettingValues", "settings");

        builder.Property(x => x.Name)
            .HasMaxLength(128)
            .IsRequired();

        // The overridden properties as a JSON object. jsonb rejects malformed JSON at the database.
        builder.Property(x => x.Values)
            .HasColumnType("jsonb")
            .IsRequired();

        // At most one row per scope and setting. Nulls are compared as equal (PostgreSQL 15+ NULLS NOT
        // DISTINCT) so platform rows (TenantId null) are unique among themselves too. The database name
        // ends in "Name" because a violation is reported against the last segment. The leading TenantId
        // column also serves every "overrides of the active scope" read.
        builder.HasIndex(x => new { x.TenantId, x.Name })
            .IsUnique()
            .AreNullsDistinct(false)
            .HasDatabaseName("IX_SettingValues_TenantId_Name");
    }
}
