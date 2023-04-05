using System.ComponentModel.DataAnnotations;
using EasyForNet.Domain.Entities.Audit;
using Microsoft.EntityFrameworkCore;

namespace CSharpTemplate.Common.Identity.Entities;

[Index(nameof(Username), IsUnique = true)]
[Index(nameof(Email), IsUnique = true)]
public class User : AuditEntity<long>
{
    [Required] [StringLength(16)] public string Username { get; set; } = string.Empty;

    [Required]
    [StringLength(1024)]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required] [StringLength(64)] public string Name { get; set; } = string.Empty;

    [Required] [StringLength(128)] public string HashedPassword { get; set; } = string.Empty;

    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}