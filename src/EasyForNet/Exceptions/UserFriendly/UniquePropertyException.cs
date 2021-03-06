namespace EasyForNet.Exceptions.UserFriendly
{
    public class UniquePropertyException : UserFriendlyException
    {
        public string PropertyName { get; }

        public UniquePropertyException(string propertyName, string message = null)
            : base(string.IsNullOrWhiteSpace(message) ? $"Duplicate of {propertyName} not allowed" : message)
        {
            PropertyName = propertyName;
        }
    }
}