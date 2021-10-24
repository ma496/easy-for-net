namespace EasyForNet.Exceptions
{
    public class UniquePropertyException : AppException
    {
        public string PropertyName { get; }

        public UniquePropertyException(string propertyName, string message = null) 
            : base(string.IsNullOrWhiteSpace(message) ? $"Duplicate of {propertyName} not allowed" : message)
        {
            PropertyName = propertyName;
        }
    }
}