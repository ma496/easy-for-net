namespace CSharpTemplate.Common.Identity;

public class LoginUserOutput
{
    public string Message { get; set; } = string.Empty;
    public bool IsSuccess { get; set; }
    public DateTime? ExpireDate { get; set; }
    public string Token { get; set; } = string.Empty;
}
