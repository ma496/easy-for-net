namespace Backend.Features.Tenancy.Endpoints.Editions;

using Backend.Features.Tenancy.Core;

/// <summary>
/// This endpoint that handles <c>DELETE /editions/{id}</c> to retire a plan.
/// </summary>
/// <remarks>
/// A plan any tenant is still on is refused rather than deleted. Deleting it would take its feature
/// values out of the chain, and every tenant on it would silently drop to whatever the deployment and
/// the definitions declare - a downgrade nobody asked for and nobody would see. Moving the tenants off
/// the plan first makes that an explicit decision.
/// </remarks>
sealed class EditionDeleteEndpoint(IEditionService editionService) : Endpoint<EditionDeleteRequest, EditionDeleteResponse>
{
    public override void Configure()
    {
        Delete("{id}");
        Group<EditionsGroup>();
        Permissions(Allow.Edition_Delete);
    }

    public override async Task HandleAsync(EditionDeleteRequest request, CancellationToken cancellationToken)
    {
        var entity = await editionService.GetByIdAsync(request.Id, cancellationToken);
        if (entity == null)
        {
            await Send.NotFoundAsync(cancellationToken);
            return;
        }

        if (await editionService.TenantCountAsync(entity.Id, cancellationToken) > 0)
        {
            ThrowError(IEditionService.InUseMessage, ErrorCodes.EditionInUse);
        }

        await editionService.DeleteAsync(entity, cancellationToken);

        await Send.ResponseAsync(new EditionDeleteResponse { Id = entity.Id }, cancellation: cancellationToken);
    }
}

/// <summary>
/// Request payload identifying the edition to delete.
/// </summary>
sealed class EditionDeleteRequest : BaseDto<Guid>
{
}

/// <summary>
/// Response payload echoing the identity of the deleted edition.
/// </summary>
public sealed class EditionDeleteResponse : BaseDto<Guid>
{
}
