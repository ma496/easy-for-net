namespace EasyForNet.Exceptions.UserFriendly;

public class DuplicateException : UserFriendlyException
{
    public string PropertyName { get; }
    public object PropertyValue { get; set; }

    public DuplicateException(string propertyName, string message = null)
        : base(string.IsNullOrWhiteSpace(message) ? $"Value of {propertyName} field already exist" : message)
    {
        PropertyName = propertyName;
    }
}