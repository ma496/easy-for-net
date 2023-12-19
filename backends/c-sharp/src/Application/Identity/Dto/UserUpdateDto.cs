namespace EasyForNet.Application.Identity.Dto;
public class UserUpdateDto
{
    public string Email { get; set; } = null!;
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
}
