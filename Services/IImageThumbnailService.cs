namespace WebFileBrowser.Services;

public interface IImageThumbnailService
{
    public Task<byte[]> GetImageThumbnail(string share, string path);
    public Task SetThumbnailCacheAsync(string filePath, byte[] thumbnailData);
    public Task FlushThumbnailFromCache(string share, string path);
    public string GetThumbnailImageMimeType();
}