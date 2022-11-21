namespace EasyForNet.Domain.Entities
{
    public abstract class Entity<TKey> : IEntity<TKey>
    {
        public virtual TKey Id { get; set; }

        public virtual bool IsTransient()
        {
            return Id.Equals(default(TKey));
        }

        public static bool operator ==(Entity<TKey> left, Entity<TKey> right)
        {
            if (ReferenceEquals(left, right))
                return true;

            if ((object) left == null || (object) right == null)
                return false;

            return left.Equals(right);
        }

        public static bool operator !=(Entity<TKey> left, Entity<TKey> right)
        {
            return !(left == right);
        }

        public override bool Equals(object obj)
        {
            if (!(obj is Entity<TKey> compareTo))
                return false;

            if (ReferenceEquals(this, compareTo))
                return true;

            if (GetType() != compareTo.GetType())
                return false;

            return !IsTransient() && !compareTo.IsTransient() && Id.Equals(compareTo.Id);
        }

        public override int GetHashCode()
        {
            // ReSharper disable once NonReadonlyMemberInGetHashCode
            return Id.GetHashCode();
        }

        public override string ToString()
        {
            return $"[{GetType().Name} {Id}]";
        }
    }
}