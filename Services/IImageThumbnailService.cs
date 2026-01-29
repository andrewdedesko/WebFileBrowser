namespace WebFileBrowser.Services;

public interface IImageThumbnailService
{
    public Task<byte[]> GetImageThumbnail(string share, string path);
    public Task SetThumbnailCacheAsync(string filePath, byte[] thumbnailData);
}