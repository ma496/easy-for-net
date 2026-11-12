using Backend.Features.Settings.Core;

namespace Backend.Features.Settings.Endpoints.Settings;

/// <summary>
/// This endpoint that handles <c>GET /settings</c> to list every declared setting as it resolves:
/// each property's effective value and whether it came from the stored override or the default (code
/// or configured). A secret property is reported with no value, only whether one is set.
/// </summary>
sealed class SettingListEndpoint(ISettingDefinitionCatalogue catalogue, ISettingValueService settingValueService)
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
        var items = new List<SettingDto>();
        foreach (var definition in catalogue.GetAll())
        {
            var resolved = await settingValueService.ResolveAsync(definition, cancellationToken);
            items.Add(SettingDto.From(resolved));
        }

        await Send.ResponseAsync(new SettingListResponse { Items = items }, cancellation: cancellationToken);
    }
}

/// <summary>Response payload listing every declared setting as it resolves.</summary>
public sealed class SettingListResponse
{
    public List<SettingDto> Items { get; set; } = [];
}