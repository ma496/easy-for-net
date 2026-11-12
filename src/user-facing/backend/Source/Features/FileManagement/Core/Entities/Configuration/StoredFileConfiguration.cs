using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Backend.Features.FileManagement.Core.Entities.Configuration;

/// <summary>
/// EF Core entity configuration for <see cref="StoredFile"/>, mapping it to the
/// <c>filemanagement.StoredFiles</c> table, making the generated stored file name unique so that one
/// name resolves to exactly one row, and indexing the account an account-owned file belongs to.
/// </summary>
public class StoredFileConfiguration : IEntityTypeConfiguration<StoredFile>
{
    /// <summary>
    /// Configures the table mapping, schema, the unique stored file name, and the owner index.
    /// </summary>
    /// <param name="builder">The builder used to configure the <see cref="StoredFile"/> entity type.</param>
    public void Configure(EntityTypeBuilder<StoredFile> builder)
    {
        builder.ToTable("StoredFiles", "filemanagement");

        builder.HasKey(x => x.Id);

        // The stored file name is the key a download or delete request supplies, so it has to resolve
        // to exactly one row. Named explicitly to keep the name stable, since a violation is reported
        // against the last underscore-separated segment lower-cased, here "filename".
        builder.HasIndex(x => x.FileName)
            .IsUnique()
            .HasDatabaseName("IX_StoredFiles_FileName");

        builder.HasIndex(x => x.OwnerUserId);
    }
}
