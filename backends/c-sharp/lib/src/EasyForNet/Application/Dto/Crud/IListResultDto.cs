using System.Collections.Generic;

namespace EasyForNet.Application.Dto.Crud;

public interface IListResultDto<T>
{
    IReadOnlyList<T> Items { get; set; }
}
