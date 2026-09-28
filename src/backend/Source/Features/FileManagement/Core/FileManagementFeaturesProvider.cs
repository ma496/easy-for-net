namespace Backend.Features.FileManagement.Core;

/// <summary>
/// Declares the entitlement features for the File Management feature: whether the tenant's plan
/// includes file storage at all, and how large one upload may be.
/// </summary>
/// <remarks>
/// This is the entitlement declaration, not the slice's DI module - that is
/// <see cref="Backend.Features.FileManagement.FileManagementFeature"/>. Both features ship enabled, so
/// a generated project behaves as though the entitlement system were not there until it decides to
/// sell something.
/// </remarks>
public class FileManagementFeaturesProvider : IFeatureDefinitionProvider
{
    public string GroupName => "File Management";

    public void Define(FeatureDefinitionContext context)
    {
        var fileStorage = context.AddFeature(
            FeatureNames.FileManagement_Enabled,
            "File storage",
            BooleanValidator.TrueValue,
            description: "Whether the tenant may upload and keep files.");

        fileStorage.AddChild(
            FeatureNames.FileManagement_MaxFileSizeMb,
            "Maximum file size (MB)",
            "25",
            new FreeTextValueType(new NumericValidator(1, 1024)),
            "The largest single upload the tenant may make.");
    }
}
