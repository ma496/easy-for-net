using Backend.Features.Seo.Core.Entities;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Backend.Features.Seo.Core.Entities.Configuration;

/// <summary>
/// EF Core entity configuration for <see cref="SeoPage"/>, mapping it to the <c>seo.SeoPages</c> table
/// with the column lengths <see cref="SeoConstants"/> declares and one live page per (path, culture).
/// </summary>
public class SeoPageConfiguration : IEntityTypeConfiguration<SeoPage>
{
    /// <summary>
    /// Configures the table mapping, schema, column lengths, and the unique (path, culture) index over
    /// pages that are not deleted.
    /// </summary>
    /// <param name="builder">The builder used to configure the <see cref="SeoPage"/> entity type.</param>
    public void Configure(EntityTypeBuilder<SeoPage> builder)
    {
        builder.ToTable("SeoPages", "seo");

        builder.Property(x => x.Path).HasMaxLength(SeoConstants.PathMaxLength).IsRequired();
        builder.Property(x => x.Culture).HasMaxLength(16);
        builder.Property(x => x.Title).HasMaxLength(SeoConstants.TitleMaxLength);
        builder.Property(x => x.Description).HasMaxLength(SeoConstants.DescriptionMaxLength);
        builder.Property(x => x.Keywords).HasMaxLength(SeoConstants.KeywordsMaxLength);
        builder.Property(x => x.CanonicalUrl).HasMaxLength(SeoConstants.UrlMaxLength);
        builder.Property(x => x.OgTitle).HasMaxLength(SeoConstants.TitleMaxLength);
        builder.Property(x => x.OgDescription).HasMaxLength(SeoConstants.DescriptionMaxLength);
        builder.Property(x => x.TwitterCard).HasMaxLength(32).IsRequired();
        builder.Property(x => x.ChangeFrequency).HasMaxLength(16);
        builder.Property(x => x.Priority).HasPrecision(3, 2);
        builder.Property(x => x.JsonLd).HasMaxLength(SeoConstants.JsonLdMaxLength);

        // One live page per path and culture. Nulls compare as equal (PostgreSQL 15+ NULLS NOT DISTINCT),
        // so the culture-neutral page is unique too, and a deleted page frees its slot. The endpoints
        // answer a clash with seoPageAlreadyExists before the index is reached; the index is the backstop.
        builder.HasIndex(x => new { x.Path, x.Culture })
            .IsUnique()
            .AreNullsDistinct(false)
            .HasFilter("NOT \"IsDeleted\"")
            .HasDatabaseName("IX_SeoPages_Path_Culture");
    }
}
