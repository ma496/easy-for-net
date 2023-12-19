using Microsoft.AspNetCore.Identity;

namespace EasyForNet.Infrastructure.Identity;

public class ApplicationUser : IdentityUser
{
    public string? FirstName { get; private set; }
    public string? LastName { get; private set; }

    private ApplicationUser(string userName, string email) : base(userName)
    {
        Email = email;
    }

    public static ApplicationUser Create(string userName, string email, string? firstName = null, string? lastName = null)
    {
        var user = new ApplicationUser(userName, email)
        {
            FirstName = firstName,
            LastName = lastName
        };
        return user;
    }

    public void Update(string email, string? firstName = null, string? lastName = null)
    {
        Email = email;
        FirstName = firstName;
        LastName = lastName;
    }
}
