namespace Backend.ShareData.Entities.Base;

/// <summary>
/// Base class for domain value objects whose equality is defined by the
/// components that make them up rather than by reference identity.
/// </summary>
public abstract class ValueObject
{
    /// <summary>
    /// Returns the individual values that, taken together, uniquely identify
    /// this value object for equality comparisons.
    /// </summary>
    protected abstract IEnumerable<object> GetEqualityComponents();

    /// <summary>
    /// Compares this value object to another by component-wise equality.
    /// </summary>
    /// <param name="obj">The other object to compare against.</param>
    /// <returns><c>true</c> if the other object is the same type and every equality component matches; otherwise <c>false</c>.</returns>
    public override bool Equals(object? obj)
    {
        if (obj == null || obj.GetType() != GetType())
            return false;

        var other = (ValueObject)obj;
        return GetEqualityComponents()
            .SequenceEqual(other.GetEqualityComponents());
    }

    /// <summary>
    /// Computes a hash code derived from the equality components so that
    /// value objects behave correctly in hash-based collections.
    /// </summary>
    /// <returns>A combined hash code of all equality components.</returns>
    public override int GetHashCode()
    {
        return GetEqualityComponents()
            .Aggregate(1, (current, obj) =>
            {
                return HashCode.Combine(current, obj);
            });
    }

    /// <summary>
    /// Determines whether two value objects are equal by component-wise comparison.
    /// </summary>
    /// <param name="a">The left-hand value object.</param>
    /// <param name="b">The right-hand value object.</param>
    /// <returns><c>true</c> if both are equal by value; otherwise <c>false</c>.</returns>
    public static bool operator ==(ValueObject? a, ValueObject? b)
    {
        if (ReferenceEquals(a, null) && ReferenceEquals(b, null))
            return true;

        if (ReferenceEquals(a, null) || ReferenceEquals(b, null))
            return false;

        return a.Equals(b);
    }

    /// <summary>
    /// Determines whether two value objects are not equal by component-wise comparison.
    /// </summary>
    /// <param name="a">The left-hand value object.</param>
    /// <param name="b">The right-hand value object.</param>
    /// <returns><c>true</c> if the value objects differ; otherwise <c>false</c>.</returns>
    public static bool operator !=(ValueObject? a, ValueObject? b)
    {
        return !(a == b);
    }
}

