namespace Backend.Features.Identity.Core.Entities.Configuration;

using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>
/// EF Core entity configuration for <see cref="AuthToken"/>, mapping it to the <c>identity.AuthTokens</c> table
/// and defining its relationship to <see cref="User"/>.
/// </summary>
public class AuthTokenConfiguration : IEntityTypeConfiguration<AuthToken>
{
    /// <summary>
    /// Configures the table mapping, schema, and user relationship for the <see cref="AuthToken"/> entity.
    /// </summary>
    /// <param name="builder">The builder used to configure the <see cref="AuthToken"/> entity type.</param>
    public void Configure(EntityTypeBuilder<AuthToken> builder)
    {
        builder.ToTable("AuthTokens", "identity");

        builder.Property(at => at.SessionId).HasMaxLength(64);

        builder.HasIndex(at => new { at.UserId, at.SessionId });

        builder.HasOne(at => at.User)
            .WithMany(u => u.AuthTokens)
            .HasForeignKey(at => at.UserId);
    }
}
