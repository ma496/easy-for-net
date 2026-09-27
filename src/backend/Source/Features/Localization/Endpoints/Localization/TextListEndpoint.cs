namespace Backend.Features.Localization.Endpoints.Localization;

using Backend.Features.Localization.Core;

/// <summary>
/// This endpoint that handles <c>GET /localization/texts</c> to page through one culture's shipped
/// translation keys for the admin text editor, each with its shipped value, what the acting scope
/// inherits, and its own override if it has one.
/// </summary>
/// <remarks>
/// The key universe is the shipped English file - the out-of-scope note on creating keys the shipped
/// file does not declare is what keeps this list closed - and paging happens in memory over that
/// bounded set rather than in the database.
/// </remarks>
sealed class TextListEndpoint(ILocalizationService localizationService) : Endpoint<TextListRequest, TextListResponse>
{
    public override void Configure()
    {
        Get("texts");
        Group<LocalizationGroup>();
        Permissions(Allow.Localization_View);
    }

    public override async Task HandleAsync(TextListRequest request, CancellationToken cancellationToken)
    {
        var page = await localizationService.GetTextsAsync(
            request.Culture, request.Filter, request.OnlyOverridden, request.Page, request.PageSize, cancellationToken);

        await Send.ResponseAsync(new TextListResponse
        {
            Items = [.. page.Items.Select(row => new TextListItemDto
            {
                Key = row.Key,
                DefaultValue = row.DefaultValue,
                InheritedValue = row.InheritedValue,
                Value = row.Value,
            })],
            Total = page.Total,
        }, cancellation: cancellationToken);
    }
}

/// <summary>Request payload for paging through one culture's text overrides.</summary>
sealed class TextListRequest
{
    public string Culture { get; set; } = null!;
    public string? Filter { get; set; }
    public bool OnlyOverridden { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}

/// <summary>
/// FluentValidation rules requiring a shipped culture and sane paging bounds. The resource store is
/// injected so the shipped-culture check reflects what is actually embedded rather than a hard-coded
/// list, which matters once a single-language project ships only English.
/// </summary>
sealed class TextListValidator : Validator<TextListRequest>
{
    public TextListValidator(ILocalizationResourceStore resourceStore)
    {
        RuleFor(x => x.Culture)
            .NotEmpty()
            .Must(culture => resourceStore.ShippedCultures.Contains(culture, StringComparer.OrdinalIgnoreCase))
            .WithMessage("The culture is not shipped.");
        RuleFor(x => x.Page).GreaterThan(0);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}

/// <summary>Response payload wrapping a page of <see cref="TextListItemDto"/> items with the total count.</summary>
public sealed class TextListResponse : ListDto<TextListItemDto>
{
}

/// <summary>One shipped key's shipped value, what the acting scope inherits, and its own override if it has one.</summary>
public sealed class TextListItemDto
{
    public string Key { get; set; } = null!;
    public string DefaultValue { get; set; } = null!;
    public string InheritedValue { get; set; } = null!;
    public string? Value { get; set; }
}
