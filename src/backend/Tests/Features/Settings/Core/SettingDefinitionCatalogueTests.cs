namespace Backend.Tests.Features.Settings.Core;

using Backend.Features.Settings.Core;

/// <summary>
/// Pins how the setting catalogue is composed and what it refuses. These construct the catalogue from
/// purpose-built providers rather than booting the host, so a declaration the application ships cannot
/// make them fail - and a refusal here is a refusal at startup, since the host composes the catalogue
/// while it starts.
/// </summary>
public class SettingDefinitionCatalogueTests
{
    [Fact]
    public void Composes_Every_Provider_And_Finds_By_Name_And_Type()
    {
        var catalogue = Build(new AlphaProvider(), new BetaProvider());

        catalogue.GetAll().Select(x => x.Name).Should().Equal("Alpha", "Beta");
        catalogue.GetOrNull("alpha")!.Type.Should().Be<AlphaSettings>("names are matched case-insensitively, as the route matches them");
        catalogue.Get(typeof(BetaSettings)).Name.Should().Be("Beta");
        catalogue.GetOrNull("Nothing").Should().BeNull();
    }

    [Fact]
    public void Properties_And_Defaults_Come_From_The_Class()
    {
        var alpha = Build(new AlphaProvider()).Get(typeof(AlphaSettings));

        alpha.Properties.Select(x => x.Name).Should().Equal("count", "title");
        var defaults = alpha.CreateDefaultValues();
        defaults["count"]!.GetValue<int>().Should().Be(5);
        defaults["title"]!.GetValue<string>().Should().Be("alpha");
    }

    [Fact]
    public void Unregistered_Type_Throws()
    {
        var act = () => Build(new AlphaProvider()).Get(typeof(BetaSettings));

        act.Should().Throw<InvalidOperationException>().WithMessage($"*{nameof(BetaSettings)}*not registered*");
    }

    [Fact]
    public void Duplicate_Name_Throws()
    {
        var act = () => Build(new AlphaProvider(), new DuplicateNameProvider());

        act.Should().Throw<InvalidOperationException>().WithMessage("*'Alpha'*more than once*");
    }

    [Fact]
    public void Duplicate_Type_Throws()
    {
        var act = () => Build(new AlphaProvider(), new DuplicateTypeProvider());

        act.Should().Throw<InvalidOperationException>().WithMessage($"*{nameof(AlphaSettings)}*registered as both*");
    }

    [Fact]
    public void Invalid_Default_Throws()
    {
        var act = () => Build(new InvalidDefaultProvider());

        act.Should().Throw<InvalidOperationException>().WithMessage("*'Gamma'*fails its own validator*Size*");
    }

    [Fact]
    public void Overlong_Name_Is_Refused_At_Registration()
    {
        var act = () => new SettingDefinitionContext().Add(new string('n', SettingDefinitionContext.NameMaxLength + 1), new AlphaValidator());

        act.Should().Throw<ArgumentException>();
    }

    #region Fixtures

    private static ISettingDefinitionCatalogue Build(params ISettingDefinitionProvider[] providers)
        => new SettingDefinitionCatalogue(providers);

    public sealed class AlphaSettings
    {
        public int Count { get; set; } = 5;
        public string Title { get; set; } = "alpha";
    }

    public sealed class BetaSettings
    {
        public bool On { get; set; }
    }

    public sealed class GammaSettings
    {
        public int Size { get; set; }
    }

    private sealed class AlphaValidator : AbstractValidator<AlphaSettings>
    {
        public AlphaValidator() => RuleFor(x => x.Count).GreaterThan(0);
    }

    private sealed class GammaValidator : AbstractValidator<GammaSettings>
    {
        public GammaValidator() => RuleFor(x => x.Size).GreaterThan(0);
    }

    private sealed class AlphaProvider : ISettingDefinitionProvider
    {
        public void Define(SettingDefinitionContext context) => context.Add("Alpha", new AlphaValidator());
    }

    private sealed class BetaProvider : ISettingDefinitionProvider
    {
        public void Define(SettingDefinitionContext context) => context.Add("Beta", new InlineValidator<BetaSettings>());
    }

    private sealed class DuplicateNameProvider : ISettingDefinitionProvider
    {
        public void Define(SettingDefinitionContext context) => context.Add("ALPHA", new InlineValidator<BetaSettings>());
    }

    private sealed class DuplicateTypeProvider : ISettingDefinitionProvider
    {
        public void Define(SettingDefinitionContext context) => context.Add("AlphaAgain", new AlphaValidator());
    }

    private sealed class InvalidDefaultProvider : ISettingDefinitionProvider
    {
        public void Define(SettingDefinitionContext context) => context.Add("Gamma", new GammaValidator());
    }

    #endregion
}