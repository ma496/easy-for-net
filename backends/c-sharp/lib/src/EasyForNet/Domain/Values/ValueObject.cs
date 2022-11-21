using System;
using System.Linq;

namespace EasyForNet.Domain.Values;

public abstract class ValueObject<TValueObject> : IEquatable<TValueObject>
    where TValueObject : ValueObject<TValueObject>
{
    public bool Equals(TValueObject other)
    {
        if ((object) other == null)
            return false;

        var properties = GetType().GetProperties();
        return !properties.Any() ||
               properties.All(p => Equals(p.GetValue(this, null), p.GetValue(other, null)));
    }

    public override bool Equals(object obj)
    {
        if (obj == null)
            return false;

        var item = obj as ValueObject<TValueObject>;
        return (object) item != null && Equals((TValueObject) item);
    }

    public override int GetHashCode()
    {
        const int index = 1;
        const int initialHasCode = 31;

        var properties = GetType().GetProperties();

        if (!properties.Any())
            return initialHasCode;

        var hashCode = initialHasCode;
        var changeMultiplier = false;

        foreach (var property in properties)
        {
            var value = property.GetValue(this, null);

            if (value == null)
            {
                //support {"a",null,null,"a"} != {null,"a","a",null}
                hashCode = hashCode ^ (index * 13);
                continue;
            }

            hashCode = hashCode * (changeMultiplier ? 59 : 114) + value.GetHashCode();
            changeMultiplier = !changeMultiplier;
        }

        return hashCode;
    }

    public static bool operator ==(ValueObject<TValueObject> x, ValueObject<TValueObject> y)
    {
        if (ReferenceEquals(x, y))
            return true;

        if ((object) x == null || (object) y == null)
            return false;

        return x.Equals(y);
    }

    public static bool operator !=(ValueObject<TValueObject> x, ValueObject<TValueObject> y)
    {
        return !(x == y);
    }
}