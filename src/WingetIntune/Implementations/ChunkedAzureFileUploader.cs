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
            byte[] data = await fileManager.ReadAllBytesAsync(filename, cancellationToken);
            int fileSize = data.Length;
            int chunkCount = (int)Math.Ceiling((double)fileSize / chunkSize);
            using var stream = new MemoryStream(data);
            // Not sure if this is needed, but just to be sure
            stream.Seek(0, SeekOrigin.Begin);
            using var reader = new BinaryReader(stream);

            List<string> chunks = new();

            LogFileChunkInfo(fileSize, chunkCount);

            for (int chunk = 0; chunk < chunkCount; chunk++)
            {
                string chunkId = Convert.ToBase64String(Encoding.ASCII.GetBytes(chunk.ToString("0000")));
                chunks.Add(chunkId);
                int start = chunk * chunkSize;
                int length = Math.Min(chunkSize, fileSize - start);
                byte[] chunkData = reader.ReadBytes(length);
                //byte[] chunkData = data[start..(start + length)];

                LogUploadingChunk(chunk, chunkCount, start, start + length);
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
    private partial void LogFileChunkInfo(int FileSize, int ChunkCount);

    [LoggerMessage(EventId = 202, Level = LogLevel.Debug, Message = "Uploading chunk {Chunk} of {ChunkCount} ({Start} - {End})")]
    private partial void LogUploadingChunk(int Chunk, int ChunkCount, int Start, int End);

    [LoggerMessage(EventId = 203, Level = LogLevel.Debug, Message = "Finalizing chunk upload")]
    private partial void LogFinalizingUpload();

    [LoggerMessage(EventId = 204, Level = LogLevel.Error, Message = "Failed to upload {Filename} to {SasUri}")]
    private partial void LogUploadFailed(Exception ex, string Filename, string SasUri);
}
