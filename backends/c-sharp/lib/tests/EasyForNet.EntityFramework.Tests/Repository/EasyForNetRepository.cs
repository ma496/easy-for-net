using EasyForNet.Domain.Entities;
using EasyForNet.EntityFramework.Repository;
using EasyForNet.EntityFramework.Tests.Data;
using System;

namespace EasyForNet.EntityFramework.Tests.Repository
{
    public class EasyForNetRepository<TEntity, TKey> : EfRepository<EasyForNetEntityFrameworkTestsDb, TEntity, TKey>
        where TEntity : class, IEntity<TKey>, new()
        where TKey : IComparable
    {
        public EasyForNetRepository(EasyForNetEntityFrameworkTestsDb dbContext) : base(dbContext)
        {
        }
    }

    public class EasyForNetRepository<TEntity> : EfRepository<EasyForNetEntityFrameworkTestsDb, TEntity>
        where TEntity : class
    {
        public EasyForNetRepository(EasyForNetEntityFrameworkTestsDb dbContext) : base(dbContext)
        {
        }
    }
}
