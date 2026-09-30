namespace Backend.Features.Settings.Endpoints.Settings;

using Backend.Features.Settings.Core;
using Backend.Features.Tenancy.Core;

/// <summary>
/// This endpoint that handles <c>GET /settings</c> to list every declared setting as the acting scope
/// resolves it: each property's effective value and whether it came from the tenant's own override,
/// the platform's, or the code default. In platform scope there is no tenant layer, so a value is the
/// platform's or the default.
/// </summary>
sealed class SettingListEndpoint(ISettingDefinitionCatalogue catalogue, ISettingValueService settingValueService, ITenantContext tenantContext)
    : EndpointWithoutRequest<SettingListResponse>
{
    public override void Configure()
    {
        Get("");
        Group<SettingsGroup>();
        Permissions(Allow.Settings_View);
    }

    public override async Task HandleAsync(CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.CurrentTenantId;

        var items = new List<SettingDto>();
        foreach (var definition in catalogue.GetAll())
        {
            var resolved = await settingValueService.ResolveAsync(definition, tenantId, cancellationToken);
            items.Add(SettingDto.From(resolved));
        }

        await Send.ResponseAsync(new SettingListResponse { Items = items }, cancellation: cancellationToken);
    }
}

/// <summary>Response payload listing every declared setting as the acting scope resolves it.</summary>
public sealed class SettingListResponse
{
    public List<SettingDto> Items { get; set; } = [];
}