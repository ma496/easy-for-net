#pragma warning disable CS8618


#pragma warning disable CS8618

using Efn.Infrastructure.EfPersistence.Common;

namespace Efn.Identity.Entities;

public class RefreshToken : Entity
{
    public string Token { get; set; }
    public DateTime ExpiryDate { get; set; }

    public Guid UserId { get; set; }
    public User User { get; set; }
}
