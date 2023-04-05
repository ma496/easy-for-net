using EasyForNet.Domain.Entities.Audit;

namespace CSharpTemplate.Common.Identity.Entities;

public class RolePermission : AuditEntity
{
    public long RoleId { get; set; }
    public Role Role { get; set; } = new();
    public long PermissionId { get; set; }
    public Permission Permission { get; set; } = new();
}