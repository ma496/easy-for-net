namespace Backend.Tests.FeatureManagement;

/// <summary>
/// Rules the features this template actually ships have to keep, whatever they are.
/// </summary>
/// <remarks>
/// These run against the real catalogue rather than a fixture, so they are what stops a new feature
/// being declared in a shape that would quietly withhold authority from a fresh installation.
/// </remarks>
public class ShippedFeatureTests(App app) : FeatureTestsBase(app)
{
    [Fact]
    public void The_Catalogue_Is_Not_Empty()
    {
        FeatureDefinitions.GetAll().Should().NotBeEmpty(
            "an empty catalogue would make every rule below pass without proving anything");
    }

    [Fact]
    public void Every_Feature_States_A_Default_Its_Own_Value_Type_Accepts()
    {
        FeatureDefinitions.GetAll().Should().AllSatisfy(feature =>
        {
            feature.DefaultValue.Should().NotBeNull(
                $"'{feature.Name}' would otherwise resolve to nothing when no one has set it");
            feature.ValueType.IsValid(feature.DefaultValue).Should().BeTrue(
                $"'{feature.Name}' declares a default its own value type rejects");
        });
    }

    [Fact]
    public void Every_Toggle_Defaults_To_Enabled()
    {
        FeatureDefinitions.GetAll()
            .Where(feature => feature.ValueType is ToggleValueType)
            .Should().AllSatisfy(feature => feature.DefaultValue.Should().Be(
                BooleanValidator.TrueValue,
                $"'{feature.Name}' would otherwise withhold authority from an installation that has sold nothing"));
    }

    [Fact]
    public void Every_Feature_Name_Matches_The_Group_Dot_Name_Shape()
    {
        FeatureDefinitions.GetAll().Should().AllSatisfy(feature =>
            feature.Name.Should().MatchRegex(@"^[A-Z][A-Za-z]*\.[A-Z][A-Za-z]*$",
                $"'{feature.Name}' does not read as a feature name"));
    }

    [Fact]
    public void Every_Declared_Feature_Has_A_Constant_Naming_It()
    {
        var constants = typeof(FeatureNames)
            .GetFields()
            .Where(field => field is { IsLiteral: true, IsInitOnly: false } && field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue()!)
            .ToHashSet(StringComparer.Ordinal);

        FeatureDefinitions.GetNames().Should().BeSubsetOf(constants,
            "a feature declared without a constant cannot be referenced, mirrored to the web app, or gated on");
    }
}
