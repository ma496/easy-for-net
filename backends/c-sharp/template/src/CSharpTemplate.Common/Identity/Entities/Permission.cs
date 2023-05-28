using EasyForNet.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CSharpTemplate.Common.Identity.Entities;

[Index(nameof(Name), IsUnique = true)]
public class Permission : Entity<long>
{
    public string Name { get; set; } = string.Empty;
}