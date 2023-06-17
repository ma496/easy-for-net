using EasyForNet.Application.TodoLists.Queries.ExportTodos;

namespace EasyForNet.Application.Common.Interfaces;

public interface ICsvFileBuilder
{
    byte[] BuildTodoItemsFile(IEnumerable<TodoItemRecord> records);
}
