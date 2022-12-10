using System.Collections;
using System.Linq;
using Ardalis.GuardClauses;
using EasyForNet.Application.Dto.Entities.Audit;
using EasyForNet.Domain.Entities.Audit;
using EasyForNet.Extensions;

namespace EasyForNet.EntityFramework.Helpers;

public static class MapEntityProperties
{
    /*public static void Map<TEntity, TKey, TDto>(TEntity entity, TDto dto)
        where TEntity : class, IEntity<TKey>
        where TKey : IComparable
        where TDto : class, IDto<TKey>
    {
        Guard.AgainstNull(entity, nameof(entity));
        Guard.AgainstNull(dto, nameof(dto));

        dto.Id = entity.Id;
        
        if (entity is IAuditEntity auditEntity && dto is IAuditDto auditDto)
        {
            auditDto.CreatedAt = auditEntity.CreatedAt;
            auditDto.CreatedBy = auditEntity.CreatedBy;
            auditDto.UpdatedAt = auditEntity.UpdatedAt;
            auditDto.UpdatedBy = auditEntity.UpdatedBy;
        }
    }*/

    public static void Map(object entity, object dto)
    {
        Guard.Against.Null(entity, nameof(entity));
        Guard.Against.Null(dto, nameof(dto));

        if (entity.GetType().HasProperty("Id") && dto.GetType().HasProperty("Id"))
            dto.SetPropertyValue("Id", entity.GetPropertyValue("Id"));

        if (entity is IAuditEntity auditEntity && dto is IAuditEntityDto auditDto)
        {
            auditDto.CreatedAt = auditEntity.CreatedAt;
            auditDto.CreatedBy = auditEntity.CreatedBy;
            auditDto.UpdatedAt = auditEntity.UpdatedAt;
            auditDto.UpdatedBy = auditEntity.UpdatedBy;
        }

        var entityNavigationProperties = entity.GetType().GetNavigationProperties();
        var dtoNavigationProperties = dto.GetType().GetNavigationProperties();
        foreach (var dtoNavigationProperty in dtoNavigationProperties)
        {
            var entityNavigationProperty =
                entityNavigationProperties.SingleOrDefault(p => p.Name == dtoNavigationProperty.Name);
            if (entityNavigationProperty != null && !entityNavigationProperty.PropertyType.IsEnumerableType())
                Map(entityNavigationProperty.GetValue(entity), dtoNavigationProperty.GetValue(dto));
            else if (entityNavigationProperty != null && entityNavigationProperty.PropertyType.IsEnumerableType())
            {
                var entityValues = (IList) entityNavigationProperty.GetValue(entity);
                var dtoValues = (IList) dtoNavigationProperty.GetValue(dto);

                if (entityValues != null && dtoValues != null)
                {
                    for (var i = 0; i < entityValues.Count; i++)
                    {
                        Map(entityValues[i], dtoValues[i]);
                    }
                }
            }
        }
    }
}