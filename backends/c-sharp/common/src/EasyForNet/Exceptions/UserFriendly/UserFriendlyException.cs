using System;

namespace EasyForNet.Exceptions.UserFriendly
{
    public class UserFriendlyException : Exception
    {
        public UserFriendlyException(string message) : base(message)
        {
        }
    }
}