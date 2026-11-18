using Microsoft.Extensions.Logging;
using System.Text;

namespace WingetIntune.Implementations;

public partial class ChunkedAzureFileUploader : IAzureFileUploader
{
    private const string IsoEncoding = "iso-8859-1";
    private const int DefaultChunkSize = 6 * 1024 * 1024;
    private readonly HttpClient httpClient;
    private readonly ILogger<ChunkedAzureFileUploader> logger;
    private readonly IFileManager fileManager;
    private readonly int chunkSize;

    public ChunkedAzureFileUploader(HttpClient httpClient, ILogger<ChunkedAzureFileUploader> logger, IFileManager fileManager, int chunkSize = DefaultChunkSize)
    {
        this.httpClient = httpClient;
        this.logger = logger;

        this.fileManager = fileManager;
        this.chunkSize = chunkSize;
    }

    public async Task UploadFileToAzureAsync(string filename, Uri sasUri, CancellationToken cancellationToken = default)
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNullOrEmpty(filename);
        ArgumentNullException.ThrowIfNull(sasUri);
#endif
        LogUploadingFile(filename, sasUri.ToString());

        try
        {
            long fileSize = fileManager.GetFileSize(filename);
            long chunkCount = chunkSize > 0 && fileSize > 0
                ? (fileSize + chunkSize - 1) / chunkSize
                : 0;

            List<string> chunks = new();

            LogFileChunkInfo(fileSize, chunkCount);

            using var stream = fileManager.OpenRead(filename);
            byte[] buffer = new byte[chunkSize];

            for (long chunk = 0; chunk < chunkCount; chunk++)
            {
                int start = (int)(chunk * chunkSize); // only used for logging; safe because chunkSize fits in int
                int length = chunkSize;
                long remaining = fileSize - (chunk * chunkSize);
                if (remaining < length)
                {
                    length = (int)remaining;
                }

                // Read exactly 'length' bytes from the stream into the buffer.
                int totalRead = 0;
                while (totalRead < length)
                {
                    int read = await stream.ReadAsync(buffer, totalRead, length - totalRead, cancellationToken);
                    if (read == 0)
                    {
                        throw new IOException($"Unexpected end of stream while reading {filename}");
                    }
                    totalRead += read;
                }

                byte[] chunkData = length == chunkSize ? buffer : buffer[..length];

                string chunkId = Convert.ToBase64String(Encoding.ASCII.GetBytes(chunk.ToString("00000")));
                chunks.Add(chunkId);

                LogUploadingChunk(chunk, chunkCount, chunk * chunkSize, chunk * chunkSize + length);
                await UploadChunkAsync(chunkId, chunkData, sasUri, cancellationToken);
            }

            LogFinalizingUpload();
            await FinalizeChunkUpload(sasUri, chunks, cancellationToken);
        }
        catch (Exception ex)
        {
            LogUploadFailed(ex, filename, sasUri.ToString());
            throw;
        }
    }

    private async Task UploadChunkAsync(string chunkId, byte[] chunkData, Uri sasUri, CancellationToken cancellationToken)
    {
        var content = new ByteArrayContent(chunkData);
        //var content = new StringContent(Encoding.GetEncoding(IsoEncoding).GetString(chunkData));
        content.Headers.Add("x-ms-blob-type", "BlockBlob");

        var requestUri = new UriBuilder(sasUri);
        requestUri.Query = $"{requestUri.Query}&comp=block&blockid={chunkId}";
        var request = new HttpRequestMessage(HttpMethod.Put, requestUri.Uri)
        {
            Content = content
        };
        var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private async Task FinalizeChunkUpload(Uri sasUri, List<string> chunks, CancellationToken cancellationToken)
    {
        var requestUri = new UriBuilder(sasUri);
        requestUri.Query = $"{requestUri.Query}&comp=blocklist";
        var request = new HttpRequestMessage(HttpMethod.Put, requestUri.Uri)
        {
            Content = new StringContent("<?xml version=\"1.0\" encoding=\"utf-8\"?><BlockList>" + string.Join("", chunks.Select(c => $"<Latest>{c}</Latest>")) + "</BlockList>")
        };
        var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    [LoggerMessage(EventId = 200, Level = LogLevel.Debug, Message = "Uploading {Filename} to {SasUri}")]
    private partial void LogUploadingFile(string Filename, string SasUri);

    [LoggerMessage(EventId = 201, Level = LogLevel.Debug, Message = "File is {FileSize} bytes, will be uploaded in {ChunkCount} chunks")]
    private partial void LogFileChunkInfo(long FileSize, long ChunkCount);

    [LoggerMessage(EventId = 202, Level = LogLevel.Debug, Message = "Uploading chunk {Chunk} of {ChunkCount} ({Start} - {End})")]
    private partial void LogUploadingChunk(long Chunk, long ChunkCount, long Start, long End);

    [LoggerMessage(EventId = 203, Level = LogLevel.Debug, Message = "Finalizing chunk upload")]
    private partial void LogFinalizingUpload();

    [LoggerMessage(EventId = 204, Level = LogLevel.Error, Message = "Failed to upload {Filename} to {SasUri}")]
    private partial void LogUploadFailed(Exception ex, string Filename, string SasUri);
}