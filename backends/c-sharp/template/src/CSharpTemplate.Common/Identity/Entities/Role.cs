using EasyForNet.Domain.Entities.Audit;
using Microsoft.EntityFrameworkCore;

namespace CSharpTemplate.Common.Identity.Entities;

[Index(nameof(Name), IsUnique = true)]
public class Role : AuditEntity<long>
{
    public string Name { get; set; } = string.Empty;

    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}