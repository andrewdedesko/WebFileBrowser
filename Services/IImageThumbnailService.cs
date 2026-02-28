namespace WebFileBrowser.Services;

public interface IImageThumbnailService
{
    public Task<byte[]> GetImageThumbnail(string share, string path, int size = 240, bool useCache = true, bool refreshCache = false);
    public Task SetThumbnailCacheAsync(string share, string path, int size, byte[] thumbnailData);
    public Task SetThumbnailCacheAsync(string filePath, int size, byte[] thumbnailData);
    public Task FlushThumbnailFromCache(string share, string path);
    public string GetThumbnailImageMimeType();

    public Task RefreshThumbnailAsync(string share, string path, int size = 240);
    public Task RefreshThumbnailsAsync(string share, string path, int size = 240);
}