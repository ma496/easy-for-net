namespace Backend.Features.Settings.Endpoints.Settings;

using Backend.Features.Settings.Core;

/// <summary>
/// This endpoint that handles <c>DELETE /settings/{name}</c> to remove the acting scope's own overrides
/// of one setting - the tenant's inside a tenant, the platform's in platform scope - so every property
/// follows the layer below again. Idempotent: a scope with no overrides of its own answers 204 exactly
/// as one that just had them removed does. Revokes no session.
/// </summary>
sealed class SettingDeleteEndpoint(ISettingDefinitionCatalogue catalogue, ISettingValueService settingValueService)
    : Endpoint<SettingDeleteRequest, EmptyResponse>
{
    public override void Configure()
    {
        Delete("{name}");
        Group<SettingsGroup>();
        Permissions(Allow.Settings_Update);
    }

    public override async Task HandleAsync(SettingDeleteRequest request, CancellationToken cancellationToken)
    {
        var definition = catalogue.GetOrNull(request.Name);
        if (definition is null)
        {
            this.ThrowError(ErrorCodes.SettingNotFound, StatusCodes.Status404NotFound);
        }

        await settingValueService.DeleteOwnAsync(definition, cancellationToken);
        await Send.NoContentAsync(cancellationToken);
    }
}

/// <summary>Request naming the setting whose overrides the acting scope removes.</summary>
sealed class SettingDeleteRequest
{
    public string Name { get; set; } = null!;
}

/// <summary>FluentValidation rules requiring a setting name.</summary>
sealed class SettingDeleteValidator : Validator<SettingDeleteRequest>
{
    public SettingDeleteValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(SettingDefinitionContext.NameMaxLength);
    }
}