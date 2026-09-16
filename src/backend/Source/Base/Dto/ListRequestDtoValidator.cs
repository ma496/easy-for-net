namespace Backend.Base.Dto;

/// <summary>
/// FluentValidation validator for <see cref="ListRequestDto{TId}"/>, enforcing
/// sane pagination and sort direction values when a normal paged query is requested.
/// </summary>
public class ListRequestDtoValidator<TId> : Validator<ListRequestDto<TId>>
{
    public ListRequestDtoValidator()
    {
        // The paging rules apply exactly when paging is applied: `IQueryableExtension.Process` pages
        // only when the request does not ask for everything and does not name the rows it wants, so
        // validating the values in any other case would refuse a request that is never paged - and
        // failing to validate them in this one leaves both values unbounded.
        var paged = new Func<ListRequestDto<TId>, bool>(
            request => !request.All && request.IncludeIds is null or { Count: 0 });

        RuleFor(x => x.Page).GreaterThan(0).When(paged);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100).When(paged);
        RuleFor(x => x.IncludeIds)
            .Must(ids => ids is null || ids.Count <= 100)
            .WithMessage("No more than 100 IDs may be requested.");
        RuleFor(x => x.SortDirection).IsInEnum()
            .When(x => !string.IsNullOrWhiteSpace(x.SortField));
    }
}
