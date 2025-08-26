using Microsoft.Extensions.Logging;
using System.IO.Compression;
using System.Security.Cryptography;

namespace WingetIntune.Os;

public partial class DefaultFileManager : IFileManager
{
    private readonly ILogger<DefaultFileManager> logger;
    private readonly HttpClient httpClient;

    public DefaultFileManager(ILogger<DefaultFileManager> logger, HttpClient? httpClient)
    {
        this.logger = logger;
        this.httpClient = httpClient ?? new HttpClient();
    }

    public void CopyFile(string sourcePath, string destinationPath, bool overwrite = false)
    {
        var destinationFolder = Path.GetDirectoryName(destinationPath);
        if (!Directory.Exists(destinationFolder))
            Directory.CreateDirectory(destinationFolder!);
        File.Copy(sourcePath, destinationPath, overwrite);
    }

    public void CreateFolder(string path)
    {
        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);
    }

    public string CreateFolderForPackage(string parentFolder, string packageName, string packageVersion, bool arm = false)
    {
        if (arm)
        {
            packageVersion = $"{packageVersion}-arm64";
        }
        LogCreatingFolder(parentFolder, packageName, packageVersion);
        string folder = Path.Combine(parentFolder, packageName, packageVersion);
        if (Directory.Exists(folder))
        {
            // If the directory already exists, delete it to ensure a clean state
            Directory.Delete(folder, recursive: true);
        }
        Directory.CreateDirectory(folder);
        return folder;
    }

    public void DeleteFileOrFolder(string path)
    {

#if NET8_0_OR_GREATER
        if (Path.Exists(path))
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
            else
                File.Delete(path);
        }
#else
        if (Directory.Exists(path))
            Directory.Delete(path, recursive: true);
        else if (File.Exists(path))
            File.Delete(path);
#endif
    }

    public async Task DownloadFileAsync(string url, string path, string? expectedHash = null, bool throwOnFailure = true, bool overrideFile = false, CancellationToken cancellationToken = default)
    {
        if (overrideFile || !File.Exists(path))
        {
            var directory = Path.GetDirectoryName(path);
            this.CreateFolder(directory!);
            LogDownloadingFile(url, path);
            var result = await httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Get, url), HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!result.IsSuccessStatusCode && !throwOnFailure)
            {
                return;
            }
            result.EnsureSuccessStatusCode();

            bool largeFile = result.Content.Headers.ContentLength > 50 * 1024 * 1024;

            if (largeFile)
            {
                LogDownloadingLargeFile(url, path, (result.Content.Headers.ContentLength / 1024 / 1024));
            }

            using (var fileStream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: largeFile ? 81920 : 4096, useAsync: true))
            {
                await result.Content.CopyToAsync(fileStream, cancellationToken);
                await fileStream.FlushAsync(cancellationToken);
            }

            if (!string.IsNullOrEmpty(expectedHash))
            {
                using var sha256 = SHA256.Create();
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 4096, useAsync: true);
                var hashBytes = await sha256.ComputeHashAsync(stream, cancellationToken);
                stream.Close();
                var hash = BitConverter.ToString(hashBytes).Replace("-", "");
                if (!hash.Equals(expectedHash, StringComparison.OrdinalIgnoreCase))
                {
                    File.Delete(path);
                    var ex = new CryptographicException($"Hash mismatch for {url}. Expected {expectedHash} but got {hash}");
                    LogHashMismatch(ex, url, expectedHash, hash);
                    throw ex;
                }
                LogDownloadedFileHashValid(url, hash);
            }
        }
        else if (!string.IsNullOrEmpty(expectedHash))
        {
            using var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Delete, bufferSize: 4096, useAsync: true);
            using var sha256 = SHA256.Create();
            var hashBytes = await sha256.ComputeHashAsync(fileStream, cancellationToken);
            fileStream.Close();
            var hash = BitConverter.ToString(hashBytes).Replace("-", "");
            if (!hash.Equals(expectedHash, StringComparison.OrdinalIgnoreCase))
            {
                LogHashMismatchRedownloading(path, hash, expectedHash);
                File.Delete(path);
                await DownloadFileAsync(url, path, expectedHash, throwOnFailure, overrideFile, cancellationToken);
            }
        }
        else
        {
            LogSkippingDownload(url, path);
        }
    }

    public async Task<string?> DownloadStringAsync(string url, bool throwOnFailure = true, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync(url, cancellationToken);
        if (!response.IsSuccessStatusCode && !throwOnFailure)
        {
            return null;
        }
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    public void ExtractFileToFolder(string zipPath, string destinationFolder)
    {
        using (FileStream inputStream = new FileStream(zipPath, FileMode.Open, FileAccess.Read, FileShare.None, bufferSize: 4096, useAsync: false))
        {
            using (ZipArchive archive = new ZipArchive(inputStream, ZipArchiveMode.Read, false))
            {
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    string filePath = Path.Combine(destinationFolder, entry.FullName);

                    if (!filePath.StartsWith(destinationFolder, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new IOException("Extracting Zip entry would have resulted in a file outside the specified destination directory.");
                    }

                    if (string.IsNullOrEmpty(entry.Name))
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
                        continue;
                    }

                    entry.ExtractToFile(filePath, true);
                }
            }
        }
    }

    public async Task ExtractFileToFolderAsync(string zipPath, string destinationFolder, CancellationToken cancellationToken = default)
    {
        using (FileStream inputStream = new FileStream(zipPath, FileMode.Open, FileAccess.Read, FileShare.None, bufferSize: 4096, useAsync: true))
        {
            using (ZipArchive archive = new ZipArchive(inputStream, ZipArchiveMode.Read, false))
            {
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;
                    string filePath = Path.Combine(destinationFolder, entry.FullName);

                    if (!filePath.StartsWith(destinationFolder, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new IOException("Extracting Zip entry would have resulted in a file outside the specified destination directory.");
                    }

                    string directoryName = Path.Combine(destinationFolder, Path.GetDirectoryName(entry.FullName)!);
                    if (!string.IsNullOrEmpty(directoryName))
                        Directory.CreateDirectory(directoryName);
                    if (entry.Length > 0L)
                    {
                        string destinationFileName = Path.Combine(destinationFolder, entry.FullName);
                        using (Stream entryStream = entry.Open())
                        using (FileStream fileStream = new FileStream(destinationFileName, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 4096, useAsync: true))
                        {
                            await entryStream.CopyToAsync(fileStream);
                            fileStream.Close();
                            entryStream.Close();
                        }
                    }
                }
            }
        }
    }

    public bool FileExists(string path)
    {
        LogFileExists(path);
        return File.Exists(path);
    }

    public string FindFile(string folder, string filename)
    {
        // Recursursively search for the file in the folder
        foreach (var file in Directory.GetFiles(folder, filename, SearchOption.AllDirectories))
        {
            if (Path.GetFileName(file).Equals(filename, StringComparison.OrdinalIgnoreCase))
                return file;
        }

        throw new FileNotFoundException($"Could not find file {filename} in folder {folder}");
    }

    public long GetFileSize(string path)
    {
        return new FileInfo(path).Length;
    }

    public Task<byte[]> ReadAllBytesAsync(string path, CancellationToken cancellationToken)
    {
        return File.ReadAllBytesAsync(path, cancellationToken);
    }

    public Task WriteAllBytesAsync(string path, byte[] bytes, CancellationToken cancellationToken)
    {
        LogWritingBytes(bytes.Length, path);
        return File.WriteAllBytesAsync(path, bytes, cancellationToken);
    }

    public Task WriteAllTextAsync(string path, string text, CancellationToken cancellationToken)
    {
        LogWritingText(path);
        return File.WriteAllTextAsync(path, text, cancellationToken);
    }

    [LoggerMessage(EventId = 100, Level = LogLevel.Debug, Message = "Creating folder for package {PackageName} {PackageVersion} in {Folder}")]
    private partial void LogCreatingFolder(string folder, string PackageName, string PackageVersion);

    [LoggerMessage(EventId = 101, Level = LogLevel.Debug, Message = "Checking if file exists: {Path}")]
    private partial void LogFileExists(string Path);

    [LoggerMessage(EventId = 102, Level = LogLevel.Debug, Message = "Writing {Bytes} bytes to {Path}")]
    private partial void LogWritingBytes(int Bytes, string Path);

    [LoggerMessage(EventId = 103, Level = LogLevel.Debug, Message = "Writing text to {Path}")]
    private partial void LogWritingText(string Path);

    [LoggerMessage(EventId = 104, Level = LogLevel.Debug, Message = "Downloading {Url} to {Path}")]
    private partial void LogDownloadingFile(string Url, string Path);

    [LoggerMessage(EventId = 105, Level = LogLevel.Warning, Message = "Downloading large file {Url} to {Path} with size {SizeMB}MB")]
    private partial void LogDownloadingLargeFile(string Url, string Path, long? SizeMB);

    [LoggerMessage(EventId = 106, Level = LogLevel.Error, Message = "Hash mismatch for {Url}. Expected {ExpectedHash} but got {ActualHash}")]
    private partial void LogHashMismatch(Exception ex, string Url, string ExpectedHash, string ActualHash);

    [LoggerMessage(EventId = 107, Level = LogLevel.Debug, Message = "Downloaded file {Url} has hash '{Hash}' as expected")]
    private partial void LogDownloadedFileHashValid(string Url, string Hash);

    [LoggerMessage(EventId = 108, Level = LogLevel.Warning, Message = "Previously downloaded file {Path} has hash {ActualHash} but expected {ExpectedHash}. Deleting file and re-downloading")]
    private partial void LogHashMismatchRedownloading(string Path, string ActualHash, string ExpectedHash);

    [LoggerMessage(EventId = 109, Level = LogLevel.Debug, Message = "Skipping download of {Url} to {Path} because the file already exists")]
    private partial void LogSkippingDownload(string Url, string Path);
}
