namespace Backend.Tests.Fakes;

using Backend.Features.Settings.Core;

/// <summary>
/// A setting that exists only in the test host and that nothing in the application reads, so the
/// settings suite can write its platform row without changing how any other test's requests behave.
/// It has several properties, of several types, and a validator with rules - what proving property-level
/// inheritance and write validation needs, and what <c>SigninSettings</c> (one unconstrained flag)
/// cannot show.
/// </summary>
public sealed class ProbeSettings
{
    public int Limit { get; set; } = 10;
    public string Label { get; set; } = "default";
    public bool Enabled { get; set; }
}

/// <summary>Rules the probe setting's resolved values must satisfy.</summary>
public sealed class ProbeSettingsValidator : AbstractValidator<ProbeSettings>
{
    public ProbeSettingsValidator()
    {
        RuleFor(x => x.Limit).InclusiveBetween(1, 1000);
        RuleFor(x => x.Label).NotEmpty();
    }
}

/// <summary>
/// Registers <see cref="ProbeSettings"/> as the <see cref="Name"/> setting. Added to the test host's
/// container beside the providers the application discovers, which is where the catalogue reads them.
/// </summary>
public sealed class ProbeSettingsProvider : ISettingDefinitionProvider
{
    /// <summary>The name the probe setting is registered under.</summary>
    public const string Name = "Probe";

    public void Define(SettingDefinitionContext context)
    {
        context.Add<ProbeSettings>(Name, new ProbeSettingsValidator());
    }
}