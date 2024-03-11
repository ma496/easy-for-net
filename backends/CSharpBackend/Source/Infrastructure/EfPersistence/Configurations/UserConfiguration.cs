using Efn.Identity.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Efn.Infrastructure.EfPersistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.Property(e => e.Username)
            .IsRequired()
            .HasMaxLength(50);
        builder.Property(e => e.Email)
            .IsRequired()
            .HasMaxLength(255);
        builder.Property(e => e.Password)
            .IsRequired();
        builder.Property(e => e.Name)
            .HasMaxLength(100);

        builder.HasIndex(e => e.Username)
            .IsUnique();
        builder.HasIndex(e => e.Email)
            .IsUnique();
    }
}
