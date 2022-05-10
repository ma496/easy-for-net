namespace EasyForNet.Exceptions.UserFriendly
{
    public class NoEntityException : UserFriendlyException
    {
        public NoEntityException(string entityName, object id, string message = null)
            : base(string.IsNullOrWhiteSpace(message) ? GetMessage(entityName, id) : message)
        {
        }

        private static string GetMessage(string entityName, object id)
        {
            return $"No {entityName} found with id = {id}";
        }
    }
}