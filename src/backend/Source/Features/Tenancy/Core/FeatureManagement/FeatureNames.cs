namespace Backend.Features.Tenancy.Core.FeatureManagement;

// ReSharper disable InconsistentNaming

/// <summary>
/// Centralized catalogue of feature name constants, the entitlement counterpart of
/// <see cref="Backend.Permissions.Allow"/>.
/// </summary>
/// <remarks>
/// Adding a feature means a constant here, a definition in the owning slice's
/// <c>&lt;X&gt;FeaturesProvider</c>, and a mirrored entry in the web app's <c>feature-names.ts</c>.
/// Every feature this template ships defaults to enabled, so a generated project behaves as though the
/// entitlement system were not there until it decides to sell something.
/// </remarks>
[AllowOutside]
public partial class FeatureNames
{
    public const string Identity_UserManagement = "Identity.UserManagement";
    public const string Identity_MaxUserCount = "Identity.MaxUserCount";

    public const string FileManagement_Enabled = "FileManagement.Enabled";
    public const string FileManagement_MaxFileSizeMb = "FileManagement.MaxFileSizeMb";
}
