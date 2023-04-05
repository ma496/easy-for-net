using EasyForNet.Domain.Entities.Audit;

namespace CSharpTemplate.Common.Identity.Entities;

public class UserRole : AuditEntity
{
    public long UserId { get; set; }
    public User User { get; set; } = new();
    public long RoleId { get; set; }
    public Role Role { get; set; } = new();
}