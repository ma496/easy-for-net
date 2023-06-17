using System.Globalization;
using CsvHelper.Configuration;
using EasyForNet.Application.TodoLists.Queries.ExportTodos;

namespace EasyForNet.Infrastructure.Files.Maps;

public class TodoItemRecordMap : ClassMap<TodoItemRecord>
{
    public TodoItemRecordMap()
    {
        AutoMap(CultureInfo.InvariantCulture);

        Map(m => m.Done).Convert(c => c.Value.Done ? "Yes" : "No");
    }
}
