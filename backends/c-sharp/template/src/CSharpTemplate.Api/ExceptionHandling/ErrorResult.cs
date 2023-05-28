namespace CSharpTemplate.Api.ExceptionHandling;

public class ErrorResult
{
    public string Message { get; set; } = string.Empty;

    public string? Detail { get; set; }
    public string? ErrorId { get; set; }
    public string? SupportMessage { get; set; }
    public int Status { get; set; }
}