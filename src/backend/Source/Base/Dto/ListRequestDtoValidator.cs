namespace Backend.Base.Dto;

/// <summary>
/// FluentValidation validator for <see cref="ListRequestDto{TId}"/>, enforcing
/// sane pagination and sort direction values when a normal paged query is requested.
/// </summary>
public class ListRequestDtoValidator<TId> : Validator<ListRequestDto<TId>>
{
    public ListRequestDtoValidator()
    {
        RuleFor(x => x.Page).GreaterThan(0)
            .When(x => !x.All && x.IncludeIds?.Count == 0);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100)
            .When(x => !x.All && x.IncludeIds?.Count == 0);
        RuleFor(x => x.IncludeIds)
            .Must(ids => ids is null || ids.Count <= 100)
            .WithMessage("No more than 100 IDs may be requested.");
        RuleFor(x => x.SortDirection).IsInEnum()
            .When(x => !string.IsNullOrWhiteSpace(x.SortField));
    }
}
