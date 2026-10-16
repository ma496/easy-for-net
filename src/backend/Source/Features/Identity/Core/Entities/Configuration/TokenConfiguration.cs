using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Backend.Features.Identity.Core.Entities.Configuration;

/// <summary>
/// EF Core entity configuration for <see cref="Token"/>, mapping it to the <c>identity.Tokens</c> table
/// and defining its relationship to <see cref="User"/>.
/// </summary>
public class TokenConfiguration : IEntityTypeConfiguration<Token>
{
    /// <summary>
    /// Configures the table mapping, schema, and user relationship for the <see cref="Token"/> entity.
    /// </summary>
    /// <param name="builder">The builder used to configure the <see cref="Token"/> entity type.</param>
    public void Configure(EntityTypeBuilder<Token> builder)
    {
        builder.ToTable("Tokens", "identity");

        builder.HasOne(t => t.User)
            .WithMany(u => u.Tokens)
            .HasForeignKey(t => t.UserId);
    }
}
