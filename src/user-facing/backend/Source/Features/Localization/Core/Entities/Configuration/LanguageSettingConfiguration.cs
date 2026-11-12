using Backend.Features.Localization.Core.Entities;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Backend.Features.Localization.Core.Entities.Configuration;

/// <summary>
/// EF Core entity configuration for <see cref="LanguageSetting"/>, mapping it to the
/// <c>localization.LanguageSettings</c> table. The table holds at most one row, which the service
/// reads and writes as the application's language settings.
/// </summary>
public class LanguageSettingConfiguration : IEntityTypeConfiguration<LanguageSetting>
{
    /// <summary>
    /// Configures the table mapping, schema and column lengths.
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
    }
}
