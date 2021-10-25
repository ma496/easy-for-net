using System;
using EasyForNet.Exceptions;

namespace EasyForNet.Domain.Entities
{
    public static class EntityIdValidator
    {
        public static void Validate<TKey>(TKey id, bool isAppError = true)
            where TKey : IComparable
        {
            if ((id.CompareTo(default(TKey)) == 0 || id.CompareTo(default(TKey)) < 0) && isAppError)
                throw new AppException("Id must be greater than zero");
            if ((id.CompareTo(default(TKey)) == 0 || id.CompareTo(default(TKey)) < 0) && !isAppError)
                throw new Exception("Id must be greater than zero");
        }
    }
}