#pragma warning disable CS8618

using Efn.Infrastructure.EfPersistence.Common;

namespace Efn.Identity.Entities;

public class User : AuditableEntity
{
    public string Username { get; set; }
    public string Email { get; set; }
    public string Name { get; set; }
    public string Password { get; set; }
    public int Age { get; set; }

    public List<RefreshToken> RefreshTokens { get; set; }
}
