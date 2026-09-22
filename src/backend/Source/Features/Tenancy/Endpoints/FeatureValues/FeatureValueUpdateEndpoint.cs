namespace Backend.Features.Tenancy.Endpoints.FeatureValues;

using Backend.Features.Tenancy.Core;

/// <summary>
/// This endpoint that handles <c>PUT /features</c> to set what one tenant or one edition is entitled
/// to.
/// </summary>
/// <remarks>
/// Only the features named are touched, and a <see langword="null"/> value clears the override rather
/// than storing an empty one - which is how a value is put back to whatever it inherits. The change
/// takes effect for a caller at their next session renewal, exactly as a role change does; nothing
/// here ends a session that is already running.
/// </remarks>
sealed class FeatureValueUpdateEndpoint(IFeatureDefinitionService featureDefinitionService,
                                        IFeatureValueStore featureValueStore,
                                        IEditionService editionService,
                                        ITenantService tenantService)
    : Endpoint<FeatureValueUpdateRequest, FeatureValueUpdateResponse>
{
    public override void Configure()
    {
        Put("");
        Group<FeatureValuesGroup>();
        Permissions(Allow.FeatureValue_Manage);
    }

    public override async Task HandleAsync(FeatureValueUpdateRequest request, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(request.ProviderKey, out var providerKey))
        {
            ThrowError(x => x.ProviderKey, "The provider key is not a valid identifier.", ErrorCodes.InvalidValueProvided);
        }

        var exists = request.ProviderName == FeatureValueProviderNames.Tenant
            ? await tenantService.Tenants().AsNoTracking().AnyAsync(tenant => tenant.Id == providerKey, cancellationToken)
            : await editionService.Editions().AsNoTracking().AnyAsync(edition => edition.Id == providerKey, cancellationToken);
        if (!exists)
        {
            await Send.NotFoundAsync(cancellationToken);
            return;
        }

        // Everything is checked before anything is written, so a payload with one bad value leaves the
        // provider exactly as it was rather than half-applied.
        foreach (var feature in request.Features)
        {
            var definition = featureDefinitionService.GetOrNull(feature.Name);
            if (definition is null)
            {
                ThrowError(x => x.Features, $"There is no feature named '{feature.Name}'.", ErrorCodes.FeatureNotFound);
                continue;
            }

            if (!definition.AllowsProvider(request.ProviderName))
            {
                ThrowError(x => x.Features,
                           $"The feature '{feature.Name}' cannot be set by {request.ProviderName.ToLowerInvariant()}.",
                           ErrorCodes.FeatureProviderNotAllowed);
            }

            if (!definition.ValueType.IsValid(feature.Value))
            {
                ThrowError(x => x.Features,
                           $"'{feature.Value}' is not an acceptable value for '{feature.Name}'.",
                           ErrorCodes.InvalidFeatureValue);
            }
        }

        var changed = 0;
        foreach (var feature in request.Features)
        {
            await featureValueStore.SetAsync(feature.Name, feature.Value, request.ProviderName, request.ProviderKey, cancellationToken);
            changed++;
        }

        await Send.ResponseAsync(new FeatureValueUpdateResponse { Changed = changed }, cancellation: cancellationToken);
    }
}

/// <summary>
/// Request payload naming whose feature values are being set, and to what.
/// </summary>
public sealed class FeatureValueUpdateRequest
{
    public string ProviderName { get; set; } = null!;
    public string ProviderKey { get; set; } = null!;
    public List<FeatureValueDto> Features { get; set; } = [];
}

/// <summary>
/// One feature and the value it is being set to, or <see langword="null"/> to clear the override.
/// </summary>
public sealed class FeatureValueDto
{
    public string Name { get; set; } = null!;
    public string? Value { get; set; }
}

/// <summary>
/// FluentValidation rules restricting the request to the providers whose values are actually stored.
/// </summary>
sealed class FeatureValueUpdateValidator : Validator<FeatureValueUpdateRequest>
{
    public FeatureValueUpdateValidator()
    {
        RuleFor(x => x.ProviderKey).NotEmpty();
        RuleFor(x => x.ProviderName)
            .NotEmpty()
            .Must(FeatureValueProviderNames.Storable.Contains)
            .WithMessage("Feature values can only be set for a tenant or an edition.");
        RuleForEach(x => x.Features).ChildRules(feature => feature.RuleFor(x => x.Name).NotEmpty());
    }
}

/// <summary>
/// Response payload reporting how many features the request touched.
/// </summary>
public sealed class FeatureValueUpdateResponse
{
    public int Changed { get; set; }
}
