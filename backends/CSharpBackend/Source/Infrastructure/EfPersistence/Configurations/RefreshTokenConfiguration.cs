using Efn.Identity.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Efn.Infrastructure.EfPersistence.Configurations;

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.Property(e => e.Token)
            .IsRequired();
        builder.Property(e => e.ExpiryDate)
            .IsRequired();

        builder.HasIndex(e => new
        {
            e.UserId,
            e.Token,
            e.ExpiryDate
        })
            .IsUnique();

        builder.HasOne(e => e.User)
            .WithMany(e => e.RefreshTokens)
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
