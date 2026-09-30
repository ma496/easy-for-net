namespace Backend.Tests.Features.Settings.Core;

using Backend.Features.Settings.Core;
using Microsoft.Extensions.Configuration;

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
    public void Configuration_Section_Overlays_The_Initializers()
    {
        var delta = BuildWith(new() { ["Delta:Host"] = "configured.example", ["Delta:Port"] = "2525" }, new DeltaProvider()).Get(typeof(DeltaSettings));

        delta.HasConfiguredDefault.Should().BeTrue();
        var defaults = delta.CreateDefaultValues();
        defaults["host"]!.GetValue<string>().Should().Be("configured.example");
        defaults["port"]!.GetValue<int>().Should().Be(2525);
        defaults["sender"]!.GetValue<string>().Should().Be("code-sender", "a key the section leaves out keeps its initializer");
    }

    [Fact]
    public void Missing_Configuration_Section_Keeps_The_Code_Default()
    {
        var delta = BuildWith(new() { ["Other:Host"] = "elsewhere.example" }, new DeltaProvider()).Get(typeof(DeltaSettings));

        delta.HasConfiguredDefault.Should().BeFalse();
        var defaults = delta.CreateDefaultValues();
        defaults["host"]!.GetValue<string>().Should().Be("code.example");
        defaults["port"]!.GetValue<int>().Should().Be(25);
    }

    [Fact]
    public void Invalid_Configured_Default_Throws()
    {
        var act = () => BuildWith(new() { ["Delta:Port"] = "0" }, new DeltaProvider());

        act.Should().Throw<InvalidOperationException>().WithMessage("*configured default*'Delta'*fails its own validator*Port*");
    }

    [Fact]
    public void Unbindable_Configured_Value_Throws()
    {
        var act = () => BuildWith(new() { ["Delta:Port"] = "not-a-number" }, new DeltaProvider());

        act.Should().Throw<InvalidOperationException>().WithMessage("*'Delta'*cannot be bound*'Delta'*");
    }

    [Fact]
    public void Secret_Property_Is_Marked()
    {
        var delta = Build(new DeltaProvider()).Get(typeof(DeltaSettings));

        delta.FindProperty("password")!.IsSecret.Should().BeTrue();
        delta.FindProperty("password")!.BoundProperties.Should().Equal(["host", "port"], "bound names are mapped to their JSON names");
        delta.FindProperty("host")!.IsSecret.Should().BeFalse();
        delta.FindProperty("host")!.BoundProperties.Should().BeEmpty();
    }

    [Fact]
    public void Secret_Bound_To_An_Unknown_Property_Throws()
    {
        var act = () => Build(new UnknownBoundProvider());

        act.Should().Throw<InvalidOperationException>().WithMessage("*'secret'*'Zeta'*bound to 'Nowhere'*");
    }

    [Fact]
    public void Secret_On_A_Non_String_Property_Throws()
    {
        var act = () => Build(new NonStringSecretProvider());

        act.Should().Throw<InvalidOperationException>().WithMessage("*'pin'*'Epsilon'*[SecretSetting]*must be a string*");
    }

    [Fact]
    public void Overlong_Name_Is_Refused_At_Registration()
    {
        var act = () => new SettingDefinitionContext().Add(new string('n', SettingDefinitionContext.NameMaxLength + 1), new AlphaValidator());

        act.Should().Throw<ArgumentException>();
    }

    #region Fixtures

    private static ISettingDefinitionCatalogue Build(params ISettingDefinitionProvider[] providers)
        => BuildWith([], providers);

    private static SettingDefinitionCatalogue BuildWith(Dictionary<string, string?> configuration, params ISettingDefinitionProvider[] providers)
        => new(providers, new ConfigurationBuilder().AddInMemoryCollection(configuration).Build());

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

    public sealed class DeltaSettings
    {
        public string Host { get; set; } = "code.example";
        public int Port { get; set; } = 25;
        public string Sender { get; set; } = "code-sender";

        [SecretSetting(nameof(Host), nameof(Port))]
        public string Password { get; set; } = string.Empty;
    }

    public sealed class ZetaSettings
    {
        public string Host { get; set; } = "zeta.example";

        [SecretSetting("Nowhere")]
        public string Secret { get; set; } = string.Empty;
    }

    public sealed class EpsilonSettings
    {
        [SecretSetting]
        public int Pin { get; set; }
    }

    private sealed class DeltaValidator : AbstractValidator<DeltaSettings>
    {
        public DeltaValidator() => RuleFor(x => x.Port).GreaterThan(0);
    }

    private sealed class DeltaProvider : ISettingDefinitionProvider
    {
        public void Define(SettingDefinitionContext context) => context.Add("Delta", new DeltaValidator()).FromConfiguration("Delta");
    }

    private sealed class UnknownBoundProvider : ISettingDefinitionProvider
    {
        public void Define(SettingDefinitionContext context) => context.Add("Zeta", new InlineValidator<ZetaSettings>());
    }

    private sealed class NonStringSecretProvider : ISettingDefinitionProvider
    {
        public void Define(SettingDefinitionContext context) => context.Add("Epsilon", new InlineValidator<EpsilonSettings>());
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