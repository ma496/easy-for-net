using System.Collections.Concurrent;
using Backend.Features.FileManagement.Core;

namespace Backend.Tests.Fakes;

/// <summary>
/// Every file name the test host stored during the run, so that whatever a test did not delete
/// can be removed when the run ends.
/// </summary>
/// <remarks>
/// Storage is a directory under the host's content root, outside anything the run cleans up on
/// its own, so a test that forgets <see cref="AppTestsBase.DeleteAfterTest"/> would otherwise leave
/// its upload behind for good. This is the safety net behind that call, not a replacement for it.
/// </remarks>
public sealed class UploadedFilesRecorder
{
    private readonly ConcurrentDictionary<string, byte> _fileNames = new();

    /// <summary>
    /// Records a file name the storage provider wrote.
    /// </summary>
    public void Record(string fileName) => _fileNames.TryAdd(fileName, 0);

    /// <summary>
    /// The file names written so far, in no particular order.
    /// </summary>
    public IReadOnlyCollection<string> FileNames => [.. _fileNames.Keys];
}

/// <summary>
/// Wraps the storage provider the host registered and records the name of every file it saves,
/// passing every call through unchanged.
/// </summary>
public sealed class RecordingStorageProvider(IStorageProvider inner, UploadedFilesRecorder recorder) : IStorageProvider
{
    /// <inheritdoc />
    public async Task<string> SaveAsync(Stream stream, string fileName, string contentType)
    {
        // Recorded before the write, so a write that fails part-way is still cleaned up.
        recorder.Record(fileName);
        var storedName = await inner.SaveAsync(stream, fileName, contentType);
        recorder.Record(storedName);
        return storedName;
    }

    /// <inheritdoc />
    public Task<Stream?> GetAsync(string fileName) => inner.GetAsync(fileName);

    /// <inheritdoc />
    public Task DeleteAsync(string fileName) => inner.DeleteAsync(fileName);

    /// <inheritdoc />
    public bool Exists(string fileName) => inner.Exists(fileName);
}
