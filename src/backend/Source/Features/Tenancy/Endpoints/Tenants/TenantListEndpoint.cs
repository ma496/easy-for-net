namespace Backend.Features.Tenancy.Endpoints.Tenants;

using Backend.ShareData.Entities;
using Backend.Features.Identity.Core;
using Backend.Features.Tenancy.Core.Entities;
using Backend.Features.Tenancy.Core;

/// <summary>
/// This endpoint that handles <c>GET /tenants</c> to return a paginated, searchable and
/// status-filterable list of the tenants the caller may see.
/// </summary>
/// <remarks>
/// Usable with no tenant established, because a tenant is the scope rather than something
/// inside one: a caller asks which tenants they may reach before - or without ever - acting in any
/// of them, so requiring an established tenant here would hide the list from the accounts that most
/// need it.
/// </remarks>
sealed class TenantListEndpoint(ITenantService tenantService,
                                ITenantAuthorizationService tenantAuthorizationService) : Endpoint<TenantListRequest, TenantListResponse>
{
    public override void Configure()
    {
        Get("");
        Group<TenantsGroup>();
        Permissions(Allow.Tenant_View);
    }

    public override async Task HandleAsync(TenantListRequest request, CancellationToken cancellationToken)
    {
        // Which tenants the caller may see is decided in one place: every tenant that is not deleted
        // for a platform account acting in no tenant, and otherwise only the tenants the caller
        // holds an active membership in. The search, the filter and the total below all narrow from
        // that query, so no request can widen the set it returns.
        var query = tenantService.Tenants().AsNoTracking();

        var search = request.Search?.Trim().ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(search))
        {
            // The display name has no normalized twin, so it is matched case-insensitively in the
            // database; the identifier is matched against the stored normalized lower-case form, which
            // is what the search term has already been reduced to.
            query = query.Where(x =>
                EF.Functions.ILike(x.Name, $"%{search}%")
                || EF.Functions.Like(x.IdentifierNormalized, $"%{search}%"));
        }

        if (request.Status.HasValue)
        {
            query = query.Where(x => x.Status == request.Status.Value);
        }

        // Counted before paging, so the total describes the whole filtered set rather than the page.
        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .Process(request)
            .ToListAsync(cancellationToken);

        // How many accounts a tenant holds is user-account data owned by the identity slice, so it is
        // asked of that slice's contract rather than counted here, and asked once for the whole page
        // rather than row by row. A tenant nobody belongs to is absent from the result, which reads as
        // the zero the row shows.
        var memberCounts = await tenantAuthorizationService.GetTenantMemberCountsAsync(
            [.. items.Select(tenant => tenant.Id)], cancellationToken);

        var dtoMapper = new TenantListDtoMapper();
        var response = new TenantListResponse
        {
            Items = [.. items.Select(tenant =>
            {
                var dto = dtoMapper.Map(tenant);
                dto.UserCount = memberCounts.GetValueOrDefault(tenant.Id);
                return dto;
            })],
            Total = total
        };

        await Send.ResponseAsync(response, cancellation: cancellationToken);
    }
}

/// <summary>
/// Request payload for the tenant list endpoint, supporting free-text search, lifecycle-status
/// filtering, and the standard pagination and sort options.
/// </summary>
sealed class TenantListRequest : ListRequestDto<Guid>
{
    public TenantStatus? Status { get; set; }
}

/// <summary>
/// FluentValidation rules for the tenant list request, inheriting the standard list-request rules and
/// whitelisting the fields the list may be sorted by.
/// </summary>
sealed class TenantListValidator : Validator<TenantListRequest>
{
    public TenantListValidator()
    {
        Include(new ListRequestDtoValidator<Guid>());
        RuleFor(request => request.Status)
            .IsInEnum()
            .When(request => request.Status.HasValue);
        // Sorting reaches the database through reflection over the entity, so a field outside this
        // whitelist has to be refused here: without the rule the request would fail as an unhandled
        // error instead of as a validation failure.
        RuleFor(request => request.SortField)
            .Must(field => string.IsNullOrWhiteSpace(field) ||
                           new[] { "Id", "Name", "Identifier", "Status", "CreatedAt", "UpdatedAt" }
                               .Contains(field, StringComparer.OrdinalIgnoreCase))
            .WithMessage("The sort field is not supported.");
    }
}

/// <summary>
/// Response payload for the tenant list endpoint, wrapping a page of <see cref="TenantListDto"/> items
/// with the total number of tenants the request matched.
/// </summary>
public sealed class TenantListResponse : ListDto<TenantListDto>
{
}

/// <summary>
/// Per-row DTO representing a tenant in list responses, carrying its display name, its identifier in
/// both the entered and the normalized form, its lifecycle status and how many accounts belong to it.
/// </summary>
public sealed class TenantListDto : AuditableDto<Guid>
{
    public bool SystemCreated { get; set; }
    public string Name { get; set; } = null!;
    public string Identifier { get; set; } = null!;
    public string IdentifierNormalized { get; set; } = null!;
    public TenantStatus Status { get; set; }

    /// <summary>
    /// The number of accounts holding an active membership of the tenant. It is filled by the endpoint
    /// from the identity slice's count rather than by the mapper, because the tenant row itself knows
    /// nothing about its members.
    /// </summary>
    public int UserCount { get; set; }
}

/// <summary>
/// This mapper that projects a <see cref="Tenant"/> entity into a <see cref="TenantListDto"/>.
/// </summary>
[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class TenantListDtoMapper
{
    [MapperIgnoreTarget(nameof(TenantListDto.UserCount))]
    public partial TenantListDto Map(Tenant entity);
}
