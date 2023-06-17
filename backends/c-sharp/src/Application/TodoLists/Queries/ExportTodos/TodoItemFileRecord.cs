using EasyForNet.Application.Common.Mappings;
using EasyForNet.Domain.Entities;

namespace EasyForNet.Application.TodoLists.Queries.ExportTodos;

public class TodoItemRecord : IMapFrom<TodoItem>
{
    public string? Title { get; init; }

    public bool Done { get; init; }
}
