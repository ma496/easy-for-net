namespace Backend.Base.Dto;

/// <summary>
/// DTO representing an uploaded image as a base64 string together with its
/// file name and MIME content type.
/// </summary>
public class ImageDto
{
    public string ImageBase64 { get; set; } = null!;
    public string FileName { get; set; } = null!;
    public string ContentType { get; set; } = null!;
}
