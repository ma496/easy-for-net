namespace Backend.Features.Settings.Endpoints.Settings;

using System.Text.Json;
using System.Text.Json.Nodes;
using Backend.Features.Settings.Core;
using Backend.Features.Tenancy.Core;
using FluentValidation.Results;

/// <summary>
/// This endpoint that handles <c>PUT /settings/{name}</c> to replace the acting scope's own overrides of
/// one setting - the tenant's row inside a tenant, the platform's in platform scope.
/// </summary>
/// <remarks>
/// The body names every property this scope overrides, and only those: the properties it leaves out
/// stop being overridden here and follow the layer below again. An empty <c>values</c> object removes
/// this scope's overrides altogether, as <c>DELETE</c> does. Each property must be one the setting
/// declares (<c>settingPropertyUnknown</c>) and its value must deserialize to that property's type
/// (<c>settingPropertyInvalid</c>); property names are matched case-insensitively and stored camelCase.
/// The setting's validator is then run against the value the scope would resolve to - its default (code or configured),
/// the platform's overrides when acting in a tenant, and the submitted ones - and a failure refuses the
/// write with a request-level <c>settingValueInvalid</c> followed by the validator's own failures, each
/// named <c>values.&lt;property&gt;</c>. A settings change revokes no session.
/// <para>
/// A secret property (<see cref="SecretSettingAttribute"/>) is never answered with its value, so a
/// client cannot send it back unchanged, and it follows its own rules instead of replace semantics:
/// left out or sent as <c>null</c>, this scope's stored value of it - if it has one - is kept; sent as
/// <c>""</c>, this scope's override of it is removed and it follows the layer below; any other string is
/// stored, encrypted. The validator runs against the result with the kept secrets included.
/// </para>
/// </remarks>
sealed class SettingUpdateEndpoint(ISettingDefinitionCatalogue catalogue, ISettingValueService settingValueService, ITenantContext tenantContext)
    : Endpoint<SettingUpdateRequest, SettingDto>
{
    /// <summary>The request property the submitted values arrive in, as its errors are named.</summary>
    private const string ValuesField = "values";

    public override void Configure()
    {
        Put("{name}");
        Group<SettingsGroup>();
        Permissions(Allow.Settings_Update);
    }

    public override async Task HandleAsync(SettingUpdateRequest request, CancellationToken cancellationToken)
    {
        var definition = catalogue.GetOrNull(request.Name);
        if (definition is null)
        {
            this.ThrowError(ErrorCodes.SettingNotFound, StatusCodes.Status404NotFound);
        }

        var ownOverrides = new JsonObject();
        var clearedSecrets = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (name, value) in request.Values)
        {
            var property = definition.FindProperty(name);
            if (property is null)
            {
                this.ThrowError($"{ValuesField}.{name}", ErrorCodes.SettingPropertyUnknown);
            }

            // A secret sent as null is one the client left untouched: its stored value is kept below.
            if (property.IsSecret && value is null)
            {
                continue;
            }

            // Stored as re-serialized from the property's declared type rather than as submitted, so the
            // row holds only the declared shape: members the type does not declare never reach the jsonb.
            if (!property.TryNormalize(value, out var normalized))
            {
                this.ThrowError($"{ValuesField}.{property.Name}", ErrorCodes.SettingPropertyInvalid);
            }

            if (property.IsSecret && normalized?.GetValue<string>() is "")
            {
                clearedSecrets.Add(property.Name);
                continue;
            }

            ownOverrides[property.Name] = normalized;
        }

        var tenantId = tenantContext.CurrentTenantId;
        await KeepUntouchedSecretsAsync(definition, tenantId, ownOverrides, clearedSecrets, cancellationToken);

        var preview = await settingValueService.PreviewAsync(definition, tenantId, ownOverrides, cancellationToken);
        var result = await definition.ValidateAsync(definition.Materialize(preview.Values), cancellationToken);
        if (!result.IsValid)
        {
            ThrowValueInvalid(definition, result);
        }

        await settingValueService.SetOwnAsync(definition, ownOverrides, cancellationToken);

        var resolved = await settingValueService.ResolveAsync(definition, tenantId, cancellationToken);
        await Send.ResponseAsync(SettingDto.From(resolved), cancellation: cancellationToken);
    }

    /// <summary>
    /// Copies into <paramref name="ownOverrides"/> this scope's stored value of every secret the request
    /// neither set nor cleared, so leaving a secret out of a save does not remove it.
    /// </summary>
    private async Task KeepUntouchedSecretsAsync(
        SettingDefinition definition,
        Guid? tenantId,
        JsonObject ownOverrides,
        HashSet<string> clearedSecrets,
        CancellationToken cancellationToken)
    {
        var untouched = definition.Properties
            .Where(property => property.IsSecret && !ownOverrides.ContainsKey(property.Name) && !clearedSecrets.Contains(property.Name))
            .ToList();
        if (untouched.Count == 0)
        {
            return;
        }

        var stored = await settingValueService.GetOwnAsync(definition, tenantId, cancellationToken);
        foreach (var (name, value) in stored)
        {
            if (definition.FindProperty(name) is { } property && untouched.Contains(property))
            {
                ownOverrides[property.Name] = value?.DeepClone();
            }
        }
    }

    /// <summary>
    /// Refuses the write: a coded, request-level <c>settingValueInvalid</c> first, so every refusal of
    /// this kind can be told apart by its code, then each of the validator's own failures - left exactly
    /// as the validator wrote them (a rule declared with an <c>ErrorCodes</c> code is localized from it,
    /// any other keeps FluentValidation's per-culture message) and named after the property as the API
    /// spells it, <c>values.&lt;camelCase property&gt;</c>.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.DoesNotReturn]
    private void ThrowValueInvalid(SettingDefinition definition, ValidationResult result)
    {
        AddError(HttpContext.ResolveEnglishFallback(ErrorCodes.SettingValueInvalid), ErrorCodes.SettingValueInvalid.Value);

        foreach (var failure in result.Errors)
        {
            // A failure on a secret never echoes the value it was given.
            var isSecret = definition.FindProperty(failure.PropertyName.Split('.')[0])?.IsSecret == true;
            ValidationFailures.Add(new ValidationFailure($"{ValuesField}.{ToJsonPath(definition, failure.PropertyName)}", failure.ErrorMessage, isSecret ? null : failure.AttemptedValue)
            {
                ErrorCode = failure.ErrorCode,
                Severity = failure.Severity,
                CustomState = failure.CustomState,
                FormattedMessagePlaceholderValues = failure.FormattedMessagePlaceholderValues
            });
        }

        throw new ValidationFailureException(ValidationFailures, "The setting's validator refused the value.");
    }

    /// <summary>
    /// The JSON spelling of a validator's property path: the first segment is the setting property's own
    /// JSON name where one matches, and every other segment is camelCased as the serializer would write it.
    /// </summary>
    private static string ToJsonPath(SettingDefinition definition, string propertyPath)
    {
        if (string.IsNullOrEmpty(propertyPath))
        {
            return propertyPath;
        }

        var segments = propertyPath.Split('.');
        segments[0] = definition.FindProperty(segments[0])?.Name ?? JsonNamingPolicy.CamelCase.ConvertName(segments[0]);
        for (var i = 1; i < segments.Length; i++)
        {
            segments[i] = JsonNamingPolicy.CamelCase.ConvertName(segments[i]);
        }

        return string.Join('.', segments);
    }
}

/// <summary>
/// Request naming the setting (from the route) and every property the acting scope overrides, as a
/// JSON object keyed by property name: <c>{ "values": { "isEmailVerificationRequired": true } }</c>.
/// </summary>
sealed class SettingUpdateRequest
{
    public string Name { get; set; } = null!;
    public JsonObject Values { get; set; } = [];
}

/// <summary>FluentValidation rules requiring a setting name and a values object.</summary>
sealed class SettingUpdateValidator : Validator<SettingUpdateRequest>
{
    public SettingUpdateValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(SettingDefinitionContext.NameMaxLength);
        RuleFor(x => x.Values).NotNull();
    }
}