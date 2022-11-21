namespace EasyForNet.Exceptions.UserFriendly;

public class UniquePropertyDeletedException : UniquePropertyException
{
    public UniquePropertyDeletedException(string propertyName, string message = null) : base(propertyName, message)
    {
    }
}