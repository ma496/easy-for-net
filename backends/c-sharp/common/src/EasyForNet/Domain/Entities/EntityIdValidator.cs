using EasyForNet.Exceptions.UserFriendly;
using System;

namespace EasyForNet.Domain.Entities
{
    public static class EntityIdValidator
    {
        public static void Validate<TKey>(TKey id, bool isAppError = true)
            where TKey : IComparable
        {
            // if id have default value or minus value
            if (id.CompareTo(default(TKey)) == 0 || id.CompareTo(default(TKey)) < 0)
            {
                var errorMsg = "Id must be greater than zero";
                if (isAppError)
                    throw new UserFriendlyException(errorMsg);
                throw new Exception(errorMsg);
            }
        }
    }
}