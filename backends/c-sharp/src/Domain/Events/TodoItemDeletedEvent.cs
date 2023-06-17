using EasyForNet.Domain.Common;
using EasyForNet.Domain.Entities;

namespace EasyForNet.Domain.Events;

public class TodoItemDeletedEvent : BaseEvent
{
    public TodoItemDeletedEvent(TodoItem item)
    {
        Item = item;
    }

    public TodoItem Item { get; }
}
