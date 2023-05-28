using Ardalis.GuardClauses;
using System.Collections.Generic;

namespace EasyForNet.Application.Dto.Crud;

public class ListResultDto<T> : IListResultDto<T>
{
    public IReadOnlyList<T> Items { get; set; }

    public ListResultDto()
    {
    }

    public ListResultDto(IReadOnlyList<T> items)
    {
        Guard.Against.NullOrEmpty(items, nameof(items));

        Items = items;
    }
}