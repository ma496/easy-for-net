#pragma warning disable CS8618
using Efn.Infrastructure.EfPersistence.Common;

namespace Efn.Features.Identity.Users;

public class User : BaseAuditableEntity
{
    public string Username { get; set; }
    public string Email { get; set; }
    public string Name { get; set; }
    public string Password { get; set; }
    public int Age { get; set; }
}
