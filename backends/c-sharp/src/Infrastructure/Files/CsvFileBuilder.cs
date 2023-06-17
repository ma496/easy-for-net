using System.Globalization;
using CsvHelper;
using EasyForNet.Application.Common.Interfaces;
using EasyForNet.Application.TodoLists.Queries.ExportTodos;
using EasyForNet.Infrastructure.Files.Maps;

namespace EasyForNet.Infrastructure.Files;

public class CsvFileBuilder : ICsvFileBuilder
{
    public byte[] BuildTodoItemsFile(IEnumerable<TodoItemRecord> records)
    {
        using var memoryStream = new MemoryStream();
        using (var streamWriter = new StreamWriter(memoryStream))
        {
            using var csvWriter = new CsvWriter(streamWriter, CultureInfo.InvariantCulture);

            csvWriter.Context.RegisterClassMap<TodoItemRecordMap>();
            csvWriter.WriteRecords(records);
        }

        return memoryStream.ToArray();
    }
}
