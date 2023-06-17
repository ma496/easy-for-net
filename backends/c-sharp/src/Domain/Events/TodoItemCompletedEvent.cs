using EasyForNet.Domain.Common;
using EasyForNet.Domain.Entities;

namespace EasyForNet.Domain.Events;

public class TodoItemCompletedEvent : BaseEvent
{
    public TodoItemCompletedEvent(TodoItem item)
    {
        Item = item;
    }

    public TodoItem Item { get; }
}
