namespace Backend.Features.Tenancy.Endpoints.FeatureValues;

using Backend.Features.Tenancy.Core;

/// <summary>
/// This endpoint that handles <c>GET /features?providerName=&amp;providerKey=</c> to return the whole
/// feature catalogue with the effective value of each, for one tenant or one edition.
/// </summary>
/// <remarks>
/// One payload carries the definition, the effective value, and the provider that value came from, so
/// the management screen can render the right control, show what is in force, and say whether it is
/// the provider's own setting or something inherited - without asking a second time.
/// </remarks>
sealed class FeatureValueGetEndpoint(IFeatureDefinitionService featureDefinitionService,
                                     IFeatureValueResolver featureValueResolver,
                                     IFeatureValueStore featureValueStore,
                                     IEditionService editionService,
                                     ITenantService tenantService)
    : Endpoint<FeatureValueGetRequest, FeatureValueGetResponse>
{
    public override void Configure()
    {
        Get("");
        Group<FeatureValuesGroup>();
        Permissions(Allow.FeatureValue_View, Allow.FeatureValue_Manage);
    }

    public override async Task HandleAsync(FeatureValueGetRequest request, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(request.ProviderKey, out var providerKey))
        {
            ThrowError(x => x.ProviderKey, "The provider key is not a valid identifier.", ErrorCodes.InvalidValueProvided);
        }

        // The provider key has to name something that exists, or the screen would happily edit values
        // for a tenant or a plan that is gone and nobody would ever read them back.
        var exists = request.ProviderName == FeatureValueProviderNames.Tenant
            ? await tenantService.Tenants().AsNoTracking().AnyAsync(tenant => tenant.Id == providerKey, cancellationToken)
            : await editionService.Editions().AsNoTracking().AnyAsync(edition => edition.Id == providerKey, cancellationToken);
        if (!exists)
        {
            await Send.NotFoundAsync(cancellationToken);
            return;
        }

        var effective = request.ProviderName == FeatureValueProviderNames.Tenant
            ? await featureValueResolver.ResolveAsync(FeatureTarget.ForTenant(providerKey), cancellationToken)
            : await featureValueResolver.ResolveForEditionAsync(providerKey, cancellationToken);

        // What this provider holds in its own right, as opposed to what it inherits. The screen needs
        // both: the effective value to show, and whether this provider set it to decide if there is an
        // override to clear.
        var own = await featureValueStore.GetAllAsync(request.ProviderName, request.ProviderKey, cancellationToken);

        var groups = featureDefinitionService.GetGroups()
            .Select(group => new FeatureGroupDto
            {
                GroupName = group.GroupName,
                DisplayName = group.DisplayName,
                Features = [.. group.Features.SelectMany(feature => Describe(feature, 0, effective, own))]
            })
            .Where(group => group.Features.Count > 0)
            .ToList();

        await Send.ResponseAsync(new FeatureValueGetResponse { Groups = groups }, cancellation: cancellationToken);
    }

    /// <summary>
    /// Flattens a feature and everything beneath it into rows the screen renders in order, each
    /// carrying the depth it should be indented by.
    /// </summary>
    private static IEnumerable<FeatureDto> Describe(FeatureDefinition feature,
                                                    int depth,
                                                    FeatureValueSet effective,
                                                    IReadOnlyDictionary<string, string> own)
    {
        yield return new FeatureDto
        {
            Name = feature.Name,
            DisplayName = feature.DisplayName,
            Description = feature.Description,
            ParentName = feature.Parent?.Name,
            Depth = depth,
            Value = effective.GetOrNull(feature.Name),
            ProviderName = effective.ProviderOf(feature.Name),
            IsOverridden = own.ContainsKey(feature.Name),
            IsEnabled = effective.IsEnabled(feature.Name),
            ValueType = new FeatureValueTypeDto
            {
                Name = feature.ValueType.Name,
                ValidatorName = feature.ValueType.Validator.Name,
                ValidatorProperties = feature.ValueType.Validator.Properties,
                Items = feature.ValueType is SelectionValueType selection
                    ? [.. selection.Items.Select(item => new FeatureSelectionItemDto
                    {
                        Value = item.Value,
                        DisplayName = item.DisplayName
                    })]
                    : []
            }
        };

        foreach (var row in feature.Children.SelectMany(child => Describe(child, depth + 1, effective, own)))
        {
            yield return row;
        }
    }
}

/// <summary>
/// Request payload naming whose feature values are wanted.
/// </summary>
sealed class FeatureValueGetRequest
{
    public string ProviderName { get; set; } = null!;
    public string ProviderKey { get; set; } = null!;
}

/// <summary>
/// FluentValidation rules restricting the request to the providers whose values are actually stored.
/// </summary>
sealed class FeatureValueGetValidator : Validator<FeatureValueGetRequest>
{
    public FeatureValueGetValidator()
    {
        RuleFor(x => x.ProviderKey).NotEmpty();
        RuleFor(x => x.ProviderName)
            .NotEmpty()
            .Must(FeatureValueProviderNames.Storable.Contains)
            .WithMessage("Feature values can only be read for a tenant or an edition.");
    }
}

/// <summary>
/// Response payload carrying the whole catalogue with each feature's effective value.
/// </summary>
public sealed class FeatureValueGetResponse
{
    public List<FeatureGroupDto> Groups { get; set; } = [];
}

/// <summary>
/// One group of features as the management screen lists them.
/// </summary>
public sealed class FeatureGroupDto
{
    public string GroupName { get; set; } = null!;
    public string DisplayName { get; set; } = null!;
    public List<FeatureDto> Features { get; set; } = [];
}

/// <summary>
/// One feature, its effective value, and where that value came from.
/// </summary>
public sealed class FeatureDto
{
    public string Name { get; set; } = null!;
    public string DisplayName { get; set; } = null!;
    public string? Description { get; set; }

    /// <summary>The feature this one refines, or <see langword="null"/> for a root feature.</summary>
    public string? ParentName { get; set; }

    /// <summary>How far to indent the row, counted from its root.</summary>
    public int Depth { get; set; }

    /// <summary>The value in force, whoever supplied it.</summary>
    public string? Value { get; set; }

    /// <summary>Which provider supplied it, so the screen can say what a value is inherited from.</summary>
    public string? ProviderName { get; set; }

    /// <summary>Whether the provider being edited set this value itself, and so has one to clear.</summary>
    public bool IsOverridden { get; set; }

    /// <summary>
    /// Whether the feature is actually in force - its own value reads as true and so does every toggle
    /// above it. A child of a switched-off parent keeps a value of its own but is not in force.
    /// </summary>
    public bool IsEnabled { get; set; }

    public FeatureValueTypeDto ValueType { get; set; } = null!;
}

/// <summary>
/// What kind of value a feature holds, and what the editor may accept for it.
/// </summary>
public sealed class FeatureValueTypeDto
{
    public string Name { get; set; } = null!;
    public string ValidatorName { get; set; } = null!;

    /// <summary>
    /// The validator's parameters - bounds, lengths, patterns - so the editor can constrain the input
    /// the same way the API will, rather than letting the administrator find out by being refused.
    /// </summary>
    public IReadOnlyDictionary<string, string> ValidatorProperties { get; set; } =
        new Dictionary<string, string>();

    /// <summary>The options on offer, for a selection feature. Empty for every other kind.</summary>
    public List<FeatureSelectionItemDto> Items { get; set; } = [];
}

/// <summary>
/// One option of a selection feature.
/// </summary>
public sealed class FeatureSelectionItemDto
{
    public string Value { get; set; } = null!;
    public string DisplayName { get; set; } = null!;
}
