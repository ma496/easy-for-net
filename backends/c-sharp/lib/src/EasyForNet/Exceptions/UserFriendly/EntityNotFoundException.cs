namespace EasyForNet.Exceptions.UserFriendly;

public class EntityNotFoundException : UserFriendlyException
{
    public EntityNotFoundException(string entityName, object key, string message = null)
        : base(string.IsNullOrWhiteSpace(message) ? GetMessage(entityName, key) : message)
    {
    }

    private static string GetMessage(string entityName, object key)
    {
        return $"No {entityName} found with id = {key}";
    }
}