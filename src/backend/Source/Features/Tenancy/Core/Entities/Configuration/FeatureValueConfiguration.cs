namespace Backend.Features.Tenancy.Core.Entities.Configuration;

using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>
/// EF Core entity configuration for <see cref="FeatureValue"/>, mapping it to the
/// <c>tenancy.FeatureValues</c> table, keeping one value per feature per provider, and indexing the
/// provider pair that every read states.
/// </summary>
public class FeatureValueConfiguration : IEntityTypeConfiguration<FeatureValue>
{
    /// <summary>
    /// Configures the table mapping, schema, column lengths and the two indexes: uniqueness of a
    /// feature within one provider, and the covering index behind the batched read that resolution
    /// performs once per session mint.
    /// </summary>
    /// <param name="builder">The builder used to configure the <see cref="FeatureValue"/> entity type.</param>
    public void Configure(EntityTypeBuilder<FeatureValue> builder)
    {
        builder.ToTable("FeatureValues", "tenancy");

        builder.Property(x => x.Name)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(x => x.Value)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(x => x.ProviderName)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(x => x.ProviderKey)
            .HasMaxLength(64)
            .IsRequired();

        builder.HasIndex(x => new { x.Name, x.ProviderName, x.ProviderKey })
            .IsUnique();

        // Resolution never asks for one value: it reads every value a provider holds in a single
        // statement, so this is the index that statement runs on.
        builder.HasIndex(x => new { x.ProviderName, x.ProviderKey });
    }
}
