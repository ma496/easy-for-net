namespace Backend.Features.Tenancy.Core.FeatureManagement;

/// <summary>
/// What kind of value a feature holds, and therefore how it is validated and how the management UI
/// renders it.
/// </summary>
/// <remarks>
/// Every feature value is stored as text. The value type is what gives that text a meaning: a toggle
/// stores <c>true</c> or <c>false</c>, a free-text feature stores whatever its validator admits, and a
/// selection stores one of a fixed set. Keeping the distinction here rather than in the storage column
/// is what lets one table hold every feature and one editor screen render all of them.
/// </remarks>
[AllowOutside]
public abstract class FeatureValueType
{
    /// <summary>
    /// Stable name of the value type, which the management UI switches on to pick an input control.
    /// </summary>
    public abstract string Name { get; }

    /// <summary>
    /// The validator that decides whether a candidate value may be stored for this feature.
    /// </summary>
    public abstract IFeatureValueValidator Validator { get; }

    /// <summary>
    /// Whether <paramref name="value"/> may be stored for a feature of this type.
    /// </summary>
    /// <param name="value">The candidate value, or <see langword="null"/> to clear the override.</param>
    /// <returns><see langword="true"/> when the value is acceptable.</returns>
    public bool IsValid(string? value) => Validator.IsValid(value);
}

/// <summary>
/// A feature that is simply on or off. The default, and the only kind a permission may be gated on.
/// </summary>
[AllowOutside]
public sealed class ToggleValueType : FeatureValueType
{
    public override string Name => "Toggle";
    public override IFeatureValueValidator Validator { get; } = new BooleanValidator();
}

/// <summary>
/// A feature whose value is typed in - a numeric limit, a label, an address.
/// </summary>
/// <param name="validator">
/// The constraint the typed value must satisfy. Left unstated anything is accepted, which is rarely
/// what a limit wants - pass a <see cref="NumericValidator"/> or a <see cref="StringLengthValidator"/>.
/// </param>
[AllowOutside]
public sealed class FreeTextValueType(IFeatureValueValidator? validator = null) : FeatureValueType
{
    public override string Name => "FreeText";
    public override IFeatureValueValidator Validator { get; } = validator ?? new AlwaysValidValidator();
}

/// <summary>
/// A feature whose value is chosen from a fixed set, such as a storage tier or a support level.
/// </summary>
/// <param name="items">The values on offer, in the order the management UI should present them.</param>
[AllowOutside]
public sealed class SelectionValueType(params SelectionItem[] items) : FeatureValueType
{
    public override string Name => "Selection";

    /// <summary>
    /// The values on offer.
    /// </summary>
    public IReadOnlyList<SelectionItem> Items { get; } = items;

    public override IFeatureValueValidator Validator { get; } = new SelectionValidator(items);
}

/// <summary>
/// One option of a <see cref="SelectionValueType"/>.
/// </summary>
/// <param name="Value">The value stored when this option is chosen.</param>
/// <param name="DisplayName">What the management UI shows for it.</param>
[AllowOutside]
public sealed record SelectionItem(string Value, string DisplayName);
