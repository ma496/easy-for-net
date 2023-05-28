using System.Diagnostics.CodeAnalysis;
using EasyForNet.Domain.Entities.Audit;

namespace EasyForNet.EntityFramework.Tests.Data.Entities;

[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
public class TagEntity : AuditEntity<long>
{
    public string Name { get; set; }
}