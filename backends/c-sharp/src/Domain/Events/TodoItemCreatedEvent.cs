using EasyForNet.Domain.Common;
using EasyForNet.Domain.Entities;

namespace EasyForNet.Domain.Events;

public class TodoItemCreatedEvent : BaseEvent
{
    public TodoItemCreatedEvent(TodoItem item)
    {
        Item = item;
    }

    public TodoItem Item { get; }
}
