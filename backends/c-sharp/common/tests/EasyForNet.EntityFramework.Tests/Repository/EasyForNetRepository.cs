using EasyForNet.Domain.Entities;
using EasyForNet.EntityFramework.Repository;
using EasyForNet.EntityFramework.Tests.Data;
using System;

namespace EasyForNet.EntityFramework.Tests.Repository
{
    public class EasyForNetRepository<TEntity, TKey> : EfRepository<EasyForNetEntityFrameworkTestsDb, TEntity, TKey>
        where TEntity : class, IEntity<TKey>
        where TKey : IComparable
    {
        public EasyForNetRepository(EasyForNetEntityFrameworkTestsDb dbContext) : base(dbContext)
        {
        }
    }
}
