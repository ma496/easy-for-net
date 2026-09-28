namespace Backend.Features.FileManagement.Core.Entities.Configuration;

using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>
/// EF Core entity configuration for <see cref="StoredFile"/>, mapping it to the
/// <c>filemanagement.StoredFiles</c> table, making the generated stored file name unique so that one
/// name resolves to exactly one row and therefore to exactly one attribution, and indexing the tenant
/// a file was uploaded in and the account an account-owned file belongs to.
/// </summary>
public class StoredFileConfiguration : IEntityTypeConfiguration<StoredFile>
{
    /// <summary>
    /// Configures the table mapping, schema, the unique stored file name, and the attribution indexes
    /// that the tenant-scoped and account-owned file lookups are restricted by.
    /// </summary>
    /// <param name="builder">The builder used to configure the <see cref="StoredFile"/> entity type.</param>
    public void Configure(EntityTypeBuilder<StoredFile> builder)
    {
        // First entity of this feature, so this introduces the feature's schema - the lowercase
        // feature name, matching "identity" and "notifications".
        builder.ToTable("StoredFiles", "filemanagement");

        builder.HasKey(x => x.Id);

        // The stored file name is the only key a download, replace or delete request supplies, so it
        // has to resolve to exactly one row: that row's attribution, not the name, decides access, and
        // a second row under the same name would make a known or guessed name ambiguous between two
        // tenants. Deliberately unfiltered - StoredFile is hard-deleted, so there are no retained rows
        // to exclude - and named explicitly to keep the name stable, since a violation is reported
        // against the last underscore-separated segment lower-cased, here "filename".
        builder.HasIndex(x => x.FileName)
            .IsUnique()
            .HasDatabaseName("IX_StoredFiles_FileName");

        // Every read of a tenant-scoped file is restricted to the active tenant by the tenant query
        // filter, which turns this column into the discriminator that filter compares on.
        builder.HasIndex(x => x.TenantId);

        // Account-owned files - a profile image and the like - carry no tenant and are found by their
        // owning account across all tenants instead, so the owner is indexed as well.
        builder.HasIndex(x => x.OwnerUserId);
    }
}