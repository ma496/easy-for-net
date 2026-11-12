using Backend.Features.Localization.Core.Entities;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Backend.Features.Localization.Core.Entities.Configuration;

/// <summary>
/// EF Core entity configuration for <see cref="LocalizationText"/>, mapping it to the
/// <c>localization.LocalizationTexts</c> table and enforcing at most one override per culture and key.
/// </summary>
public class LocalizationTextConfiguration : IEntityTypeConfiguration<LocalizationText>
{
    /// <summary>
    /// Configures the table mapping, schema, column lengths, and the unique (culture, key) index
    /// resolution and the write endpoints both rely on.
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

        // The database name ends in "Key" because a violation is reported against the last segment,
        // and "key" is the field PUT /localization/texts names.
        builder.HasIndex(x => new { x.Culture, x.Key })
            .IsUnique()
            .HasDatabaseName("IX_LocalizationTexts_Culture_Key");
    }
}
