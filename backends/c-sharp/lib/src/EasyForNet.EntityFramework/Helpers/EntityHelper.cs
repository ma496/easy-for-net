namespace EasyForNet.EntityFramework.Helpers;

public static class EntityHelper
{
    public static string EntityName<TEntity>()
        where TEntity : class
    {
        return typeof(TEntity).Name.Replace("Entity", "").ToLower();
    }
}