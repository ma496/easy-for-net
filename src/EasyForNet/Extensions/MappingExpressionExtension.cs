using System;
using System.Collections;
using System.Linq;
using AutoMapper;
using EasyForNet.Domain.Entities;
using EasyForNet.Domain.Entities.Audit;

namespace EasyForNet.Extensions
{
    public static class MappingExpressionExtension
    {
        public static void IgnoreProperties(this IMappingExpression mappingExpression, params string[] ignoreProperties)
        {
            foreach (var ignoreProperty in ignoreProperties)
            {
                mappingExpression.ForMember(ignoreProperty, opt => opt.Ignore());
            }
        }

        public static IMappingExpression<TDto, TEntity> IgnoreEntityBaseProperties<TDto, TEntity, TKey>(this
            IMappingExpression<TDto, TEntity> mappingExpression)
            where TEntity : IEntity<TKey>
            where TKey : IComparable
        {
            return mappingExpression.ForMember(e => e.Id, opt => opt.Ignore());
        }

        public static IMappingExpression<TDto, TEntity> IgnoreSoftDeleteProperties<TDto, TEntity>(this
            IMappingExpression<TDto, TEntity> mappingExpression)
            where TEntity : ISoftDeleteEntity
        {
            return mappingExpression.ForMember(e => e.IsDeleted, opt => opt.Ignore());
        }

        public static IMappingExpression<TDto, TEntity> IgnoreAuditProperties<TDto, TEntity>(this
            IMappingExpression<TDto, TEntity> mappingExpression)
            where TEntity : IAuditEntity
        {
            return mappingExpression
                .ForMember(e => e.CreatedAt, opt => opt.Ignore())
                .ForMember(e => e.CreatedBy, opt => opt.Ignore())
                .ForMember(e => e.UpdatedAt, opt => opt.Ignore())
                .ForMember(e => e.UpdatedBy, opt => opt.Ignore());
        }

        public static IMappingExpression<TDto, TEntity> IgnorePropertiesForAdd<TDto, TEntity, TKey>(this
            IMappingExpression<TDto, TEntity> mappingExpression)
            where TEntity : class, IEntity<TKey>
            where TKey : IComparable
        {
            var me = mappingExpression;

            if (typeof(IEntity<TKey>).IsAssignableFrom(typeof(TEntity)))
                me = me.ForMember(nameof(IEntity<TKey>.Id), opt => opt.Ignore());
            if (typeof(ISoftDeleteEntity).IsAssignableFrom(typeof(TEntity)))
                me = me.ForMember(nameof(ISoftDeleteEntity.IsDeleted), opt => opt.Ignore());
            if (typeof(IAuditEntity).IsAssignableFrom(typeof(TEntity)))
                me = me
                    .ForMember(nameof(IAuditEntity.CreatedAt), opt => opt.Ignore())
                    .ForMember(nameof(IAuditEntity.CreatedBy), opt => opt.Ignore())
                    .ForMember(nameof(IAuditEntity.UpdatedAt), opt => opt.Ignore())
                    .ForMember(nameof(IAuditEntity.UpdatedBy), opt => opt.Ignore());

            return me;
        }

        public static IMappingExpression<TDto, TEntity> IgnorePropertiesForUpdate<TDto, TEntity>(this
            IMappingExpression<TDto, TEntity> mappingExpression)
            where TEntity : class
        {
            var me = mappingExpression;

            if (typeof(ISoftDeleteEntity).IsAssignableFrom(typeof(TEntity)))
                me = me.ForMember(nameof(ISoftDeleteEntity.IsDeleted), opt => opt.Ignore());
            if (typeof(IAuditEntity).IsAssignableFrom(typeof(TEntity)))
                me = me
                    .ForMember(nameof(IAuditEntity.CreatedAt), opt => opt.Ignore())
                    .ForMember(nameof(IAuditEntity.CreatedBy), opt => opt.Ignore())
                    .ForMember(nameof(IAuditEntity.UpdatedAt), opt => opt.Ignore())
                    .ForMember(nameof(IAuditEntity.UpdatedBy), opt => opt.Ignore());

            return me;
        }

        public static IMappingExpression<TDto, TEntity> IgnoreEntityNavigationProperties<TDto, TEntity, TKey>(this
            IMappingExpression<TDto, TEntity> mappingExpression)
            where TEntity : class, IEntity<TKey>
            where TKey : IComparable
        {
            var me = mappingExpression;
            var properties = typeof(TEntity).GetProperties();
            foreach (var p in properties)
                if (typeof(IEntity<TKey>).IsAssignableFrom(p.PropertyType))
                    me = me.ForMember(p.Name, opt => opt.Ignore());
                else if (typeof(ICollection).IsAssignableFrom(p.PropertyType))
                    if (p.PropertyType.GenericTypeArguments.Any(t => typeof(IEntity<TKey>).IsAssignableFrom(t)))
                        me = me.ForMember(p.Name, opt => opt.Ignore());

            return me;
        }
    }
}