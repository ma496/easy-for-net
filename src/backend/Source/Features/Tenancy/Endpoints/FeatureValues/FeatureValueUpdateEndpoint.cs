namespace Backend.Features.Tenancy.Endpoints.FeatureValues;

using Backend.Features.Identity.Core.Sessions;
using Backend.Features.Tenancy.Core;

/// <summary>
/// This endpoint that handles <c>PUT /features</c> to set what one tenant or one edition is entitled
/// to.
/// </summary>
/// <remarks>
/// Only the features named are touched, and a <see langword="null"/> value clears the override rather
/// than storing an empty one - which is how a value is put back to whatever it inherits.
/// <para>
/// A session's permissions are narrowed by its tenant's plan when it is minted, so once a value has been
/// written, the sessions minted under the old plan are ended: every session acting in the tenant, or in
/// every tenant on the edition. Their members sign in again under the new plan - which is also how a
/// feature switched back on restores its permissions, with nothing to re-grant. No session in platform
/// scope is touched, because platform scope is inside no plan.
/// </para>
/// <para>
/// Every write is followed by the revocation, even one that stores what was already stored: comparing
/// first and revoking only on a difference would let a concurrent change slip between the read and the
/// write unrevoked. Each value commits on its own, so the revocation runs even when a later write fails
/// or the caller goes away, and is not itself cancelled - what has been committed has to be answered.
/// </para>
/// </remarks>
sealed class FeatureValueUpdateEndpoint(IFeatureDefinitionService featureDefinitionService,
                                        IFeatureValueStore featureValueStore,
                                        IEditionService editionService,
                                        ITenantService tenantService,
                                        ISessionRevocationService sessionRevocationService)
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
            this.ThrowError(x => x.ProviderKey, ErrorCodes.InvalidValueProvided);
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
                this.ThrowError(x => x.Features, ErrorCodes.FeatureNotFound);
                continue;
            }

            if (!definition.AllowsProvider(request.ProviderName))
            {
                this.ThrowError(x => x.Features, ErrorCodes.FeatureProviderNotAllowed);
            }

            if (!definition.ValueType.IsValid(feature.Value))
            {
                this.ThrowError(x => x.Features, ErrorCodes.InvalidFeatureValue);
            }
        }

        // Marked before each write rather than after it: a write can commit even when its call is
        // cancelled or fails on the way back, and revoking after a write that did not land costs nothing.
        var changed = 0;
        var attempted = false;
        try
        {
            foreach (var feature in request.Features)
            {
                attempted = true;
                await featureValueStore.SetAsync(feature.Name, feature.Value, request.ProviderName, request.ProviderKey, cancellationToken);
                changed++;
            }
        }
        finally
        {
            if (attempted)
            {
                await RevokeAffectedSessionsAsync(request.ProviderName, providerKey);
            }
        }

        await Send.ResponseAsync(new FeatureValueUpdateResponse { Changed = changed }, cancellation: cancellationToken);
    }

    /// <summary>
    /// Ends every session acting in the tenant named, or in every tenant on the edition named. Not
    /// cancellable, because it answers writes that have already committed.
    /// </summary>
    private async Task RevokeAffectedSessionsAsync(string providerName, Guid providerKey)
    {
        IReadOnlyList<Guid> tenantIds = providerName == FeatureValueProviderNames.Tenant
            ? [providerKey]
            : await editionService.TenantIdsAsync(providerKey, CancellationToken.None);

        foreach (var tenantId in tenantIds)
        {
            await sessionRevocationService.RevokeTenantAsync(tenantId, CancellationToken.None);
        }
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
