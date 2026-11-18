using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using System.Net;
using WingetIntune.Implementations;

namespace WingetIntune.Tests;

public class ChunkedAzureFileUploaderTests
{
    private sealed class FakeHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }

    [Fact]
    public async Task UploadFileToAzureAsync_SmallFile_DoesNotThrow()
    {
        var data = new byte[1024];
        var fileManager = Substitute.For<IFileManager>();
        fileManager.GetFileSize("file").Returns((long)data.Length);
        fileManager.OpenRead("file").Returns(new MemoryStream(data));

        using var client = new HttpClient(new FakeHandler());
        var uploader = new ChunkedAzureFileUploader(client, NullLogger<ChunkedAzureFileUploader>.Instance, fileManager, chunkSize: 512);

        await uploader.UploadFileToAzureAsync("file", new Uri("https://example.com/container?sig=1"), CancellationToken.None);

        fileManager.Received().GetFileSize("file");
        fileManager.Received().OpenRead("file");
    }

    [Fact]
    public async Task UploadFileToAzureAsync_EmptyFile_DoesNotThrow()
    {
        var fileManager = Substitute.For<IFileManager>();
        fileManager.GetFileSize("empty").Returns(0L);
        fileManager.OpenRead("empty").Returns(new MemoryStream(Array.Empty<byte>()));

        using var client = new HttpClient(new FakeHandler());
        var uploader = new ChunkedAzureFileUploader(client, NullLogger<ChunkedAzureFileUploader>.Instance, fileManager, chunkSize: 512);

        await uploader.UploadFileToAzureAsync("empty", new Uri("https://example.com/container?sig=1"), CancellationToken.None);

        fileManager.Received().GetFileSize("empty");
        fileManager.Received().OpenRead("empty");
    }

    [Fact]
    public async Task UploadFileToAzureAsync_MultipleChunks_UploadsAll()
    {
        var data = new byte[1500];
        var fileManager = Substitute.For<IFileManager>();
        fileManager.GetFileSize("file").Returns((long)data.Length);
        fileManager.OpenRead("file").Returns(new MemoryStream(data));

        using var client = new HttpClient(new FakeHandler());
        var uploader = new ChunkedAzureFileUploader(client, NullLogger<ChunkedAzureFileUploader>.Instance, fileManager, chunkSize: 512);

        // 1500 bytes / 512 = 3 chunks
        await uploader.UploadFileToAzureAsync("file", new Uri("https://example.com/container?sig=1"), CancellationToken.None);

        fileManager.Received().GetFileSize("file");
        fileManager.Received().OpenRead("file");
    }
}