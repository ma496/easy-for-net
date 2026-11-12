using Backend.Features.Settings.Core.Entities;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Backend.Features.Settings.Core.Entities.Configuration;

/// <summary>
/// EF Core entity configuration for <see cref="SettingValue"/>, mapping it to the
/// <c>settings.SettingValues</c> table and enforcing at most one row per setting.
/// </summary>
public class SettingValueConfiguration : IEntityTypeConfiguration<SettingValue>
{
    /// <summary>
    /// Configures the table mapping, schema, column types, and the unique name index.
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

        // The database name ends in "Name" because a violation is reported against the last segment.
        builder.HasIndex(x => x.Name)
            .IsUnique()
            .HasDatabaseName("IX_SettingValues_Name");
    }
}
