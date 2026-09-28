namespace Backend.Tests.FeatureManagement;

using Backend.Exceptions;

/// <summary>
/// Pins the shape of the composed feature catalogue. These run against purpose-built providers rather
/// than the ones the application ships, so a change to what this template sells cannot make them fail.
/// </summary>
public class FeatureDefinitionServiceTests
{
    [Fact]
    public void Each_Provider_Contributes_One_Group()
    {
        var service = Build(new AlphaProvider(), new BetaProvider());

        var groups = service.GetGroups();

        groups.Should().HaveCount(2);
        groups.Select(group => group.GroupName).Should().Equal("Alpha", "Beta");
        groups[0].DisplayName.Should().Be("Alpha Group", "a provider may show something other than its name");
        groups[1].DisplayName.Should().Be("Beta", "the group name stands in when no display name is stated");
    }

    [Fact]
    public void A_Provider_That_Declares_Nothing_Contributes_No_Group()
    {
        var service = Build(new AlphaProvider(), new EmptyProvider());

        service.GetGroups().Select(group => group.GroupName).Should().Equal("Alpha");
    }

    [Fact]
    public void Flattening_Keeps_Parents_As_Well_As_Children()
    {
        var service = Build(new AlphaProvider());

        service.GetAll().Select(feature => feature.Name)
               .Should().Equal("Alpha.Root", "Alpha.Child", "Alpha.Grandchild");
    }

    [Fact]
    public void A_Child_Knows_Its_Parent()
    {
        var service = Build(new AlphaProvider());

        service.Get("Alpha.Child").Parent!.Name.Should().Be("Alpha.Root");
        service.Get("Alpha.Grandchild").Parent!.Name.Should().Be("Alpha.Child");
        service.Get("Alpha.Root").Parent.Should().BeNull();
    }

    [Fact]
    public void An_Undeclared_Name_Is_Null_Or_Throws_Depending_On_How_It_Is_Asked_For()
    {
        var service = Build(new AlphaProvider());

        service.GetOrNull("Alpha.Nothing").Should().BeNull();
        var act = () => service.Get("Alpha.Nothing");
        act.Should().Throw<FeatureNotDefinedException>().Which.FeatureName.Should().Be("Alpha.Nothing");
    }

    [Fact]
    public void Feature_Names_Are_Global_So_A_Duplicate_Refuses_To_Compose()
    {
        var act = () => Build(new AlphaProvider(), new AlphaDuplicateProvider());

        act.Should().Throw<InvalidOperationException>().WithMessage("*Alpha.Root*more than once*");
    }

    [Fact]
    public void Names_Cover_Every_Node_In_Every_Group()
    {
        var service = Build(new AlphaProvider(), new BetaProvider());

        service.GetNames().Should().BeEquivalentTo(
            ["Alpha.Root", "Alpha.Child", "Alpha.Grandchild", "Beta.Toggle"]);
    }

    [Fact]
    public void A_Feature_Confined_To_Providers_Admits_Those_And_The_Default()
    {
        var service = Build(new BetaProvider());

        var feature = service.Get("Beta.Toggle");

        feature.AllowsProvider(FeatureValueProviderNames.Edition).Should().BeTrue();
        feature.AllowsProvider(FeatureValueProviderNames.Tenant).Should().BeFalse();
        feature.AllowsProvider(FeatureValueProviderNames.Default).Should()
               .BeTrue("the default is the definition's own fallback, not something a provider stored");
    }

    [Fact]
    public void An_Unconfined_Feature_Admits_Every_Provider()
    {
        var feature = Build(new AlphaProvider()).Get("Alpha.Root");

        feature.AllowsProvider(FeatureValueProviderNames.Tenant).Should().BeTrue();
        feature.AllowsProvider(FeatureValueProviderNames.Edition).Should().BeTrue();
    }

    #region Fixtures

    private static IFeatureDefinitionService Build(params IFeatureDefinitionProvider[] providers)
        => new FeatureDefinitionService(providers);

    private sealed class AlphaProvider : IFeatureDefinitionProvider
    {
        public string GroupName => "Alpha";
        public string GroupDisplayName => "Alpha Group";

        public void Define(FeatureDefinitionContext context)
        {
            var root = context.AddFeature("Alpha.Root", "Root", BooleanValidator.TrueValue);
            var child = root.AddChild("Alpha.Child", "Child", BooleanValidator.TrueValue);
            child.AddChild("Alpha.Grandchild", "Grandchild", BooleanValidator.TrueValue);
        }
    }

    private sealed class AlphaDuplicateProvider : IFeatureDefinitionProvider
    {
        public string GroupName => "AlphaAgain";

        public void Define(FeatureDefinitionContext context)
            => context.AddFeature("Alpha.Root", "Root Again", BooleanValidator.TrueValue);
    }

    private sealed class BetaProvider : IFeatureDefinitionProvider
    {
        public string GroupName => "Beta";

        public void Define(FeatureDefinitionContext context)
            => context.AddFeature("Beta.Toggle", "Toggle", BooleanValidator.TrueValue)
                      .AllowProviders(FeatureValueProviderNames.Edition);
    }

    private sealed class EmptyProvider : IFeatureDefinitionProvider
    {
        public string GroupName => "Empty";

        public void Define(FeatureDefinitionContext context)
        {
        }
    }

    #endregion
}
