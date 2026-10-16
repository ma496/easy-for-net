using Backend.Attributes;

namespace Backend.Features.FileManagement.Core;

/// <summary>
/// Abstraction over the physical storage backend used by the file management feature.
/// Implementations are responsible for persisting streams, reading them back, removing
/// them, and reporting their existence.
/// </summary>
[AllowOutside]
public interface IStorageProvider
{
    /// <summary>
    /// Persists the given stream under the supplied filename and content type.
    /// </summary>
    /// <param name="stream">The content to store.</param>
    /// <param name="fileName">The name under which the content should be saved.</param>
    /// <param name="contentType">The MIME content type associated with the file.</param>
    /// <returns>The name under which the file was stored.</returns>
    Task<string> SaveAsync(Stream stream, string fileName, string contentType);

    /// <summary>
    /// Opens a readable stream over the file with the given name, or returns
    /// <c>null</c> if no such file exists.
    /// </summary>
    /// <param name="fileName">The name of the file to retrieve.</param>
    Task<Stream?> GetAsync(string fileName);

    /// <summary>
    /// Removes the file with the given name from storage, if present.
    /// </summary>
    /// <param name="fileName">The name of the file to delete.</param>
    Task DeleteAsync(string fileName);

    /// <summary>
    /// Indicates whether a file with the given name currently exists in storage.
    /// </summary>
    /// <param name="fileName">The name of the file to check.</param>
    bool Exists(string fileName);
}
