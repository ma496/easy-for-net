namespace Backend.Tests.Features.FileManagement.Core;

using Backend.Features.FileManagement.Core;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;

public class LocalStorageProviderTests : IDisposable
{
    private readonly string _contentRoot = Path.Combine(Path.GetTempPath(), $"easy-for-net-tests-{Guid.NewGuid():N}");
    private readonly LocalStorageProvider _provider;

    public LocalStorageProviderTests()
    {
        Directory.CreateDirectory(_contentRoot);
        _provider = new LocalStorageProvider(new TestWebHostEnvironment(_contentRoot));
    }

    [Theory]
    [InlineData("../outside.txt")]
    [InlineData("..\\outside.txt")]
    [InlineData("folder/file.txt")]
    public async Task StorageOperations_RejectPathsOutsideUploads(string fileName)
    {
        var save = () => _provider.SaveAsync(Stream.Null, fileName, "text/plain");
        var get = () => _provider.GetAsync(fileName);
        var delete = () => _provider.DeleteAsync(fileName);

        await save.Should().ThrowAsync<ArgumentException>();
        await get.Should().ThrowAsync<ArgumentException>();
        await delete.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task StorageOperations_RoundTripValidFile()
    {
        const string content = "safe content";
        await using var input = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content));

        await _provider.SaveAsync(input, "safe.txt", "text/plain");
        await using (var output = await _provider.GetAsync("safe.txt"))
        using (var reader = new StreamReader(output!))
        {
            (await reader.ReadToEndAsync(TestContext.Current.CancellationToken)).Should().Be(content);
        }
        _provider.Exists("safe.txt").Should().BeTrue();

        await _provider.DeleteAsync("safe.txt");
        _provider.Exists("safe.txt").Should().BeFalse();
    }

    public void Dispose()
    {
        if (Directory.Exists(_contentRoot))
        {
            Directory.Delete(_contentRoot, recursive: true);
        }

        GC.SuppressFinalize(this);
    }

    private sealed class TestWebHostEnvironment(string contentRootPath) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "Backend.Tests";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = contentRootPath;
        public string EnvironmentName { get; set; } = "Testing";
        public string ContentRootPath { get; set; } = contentRootPath;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
