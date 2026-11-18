namespace WingetIntune;

public interface IFileManager
{
    void CopyFile(string sourcePath, string destinationPath, bool overwrite = false);

    void CreateFolder(string path);

    string CreateFolderForPackage(string parentFolder, string packageName, string packageVersion, bool arm = false);

    void DeleteFileOrFolder(string path);

    Task DownloadFileAsync(string url, string path, string? expectedHash = null, bool throwOnFailure = true, bool overrideFile = false, CancellationToken cancellationToken = default);

    Task<string?> DownloadStringAsync(string url, bool throwOnFailure = true, CancellationToken cancellationToken = default);

    void ExtractFileToFolder(string zipPath, string destinationFolder);
    Task ExtractFileToFolderAsync(string zipPath, string destinationFolder, CancellationToken cancellationToken = default);

    string FindFile(string folder, string filename);

    bool FileExists(string path);
    long GetFileSize(string path);

    Stream OpenRead(string path);

    Task<byte[]> ReadAllBytesAsync(string path, CancellationToken cancellationToken);

    Task WriteAllBytesAsync(string path, byte[] bytes, CancellationToken cancellationToken);

    Task WriteAllTextAsync(string path, string text, CancellationToken cancellationToken);
}
