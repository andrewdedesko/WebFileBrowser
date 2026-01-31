using System.Collections;
using Microsoft.Extensions.Caching.Distributed;

namespace WebFileBrowser.Services;

public class ImageThumbnailService : IImageThumbnailService {
    private readonly IShareService _shareService;
    private readonly IBrowseService _browseService;
    private readonly ImageThumbnailer _imageThumbnailer;
    private readonly VideoThumbnailer _videoThumbnailer;
    private readonly IDistributedCache _cache;

    private readonly BackgroundThumbnailQueue _thumbnailQueue;

    public ImageThumbnailService(IShareService shareService, IBrowseService browseService, IDistributedCache cache, BackgroundThumbnailQueue thumbnailQueue, ImageThumbnailer imageThumbnailer, VideoThumbnailer videoThumbnailer) {
        _shareService = shareService;
        _browseService = browseService;
        _thumbnailQueue = thumbnailQueue;
        _imageThumbnailer = imageThumbnailer;
        _videoThumbnailer = videoThumbnailer;
        _cache = cache;
    }

    public async Task<byte[]> GetImageThumbnail(string share, string path) {
        var cacheKey = $"Thumbnail:Image:{_shareService.GetPath(share, path)}";
        var cachedThumbnail = _cache.Get(cacheKey);
        if(cachedThumbnail != null) {
            return cachedThumbnail;
        }

        DistributedCacheEntryOptions cacheEntryOptions = new();
        var filePath = _shareService.GetPath(share, path);
        byte[]? data = null;
        if(Directory.Exists(filePath)) {
            // data = GetDirectoryThumbnailImageFromMiddleImageAndPreferImagesWithFaces(share, path);
            var t = _thumbnailQueue.EnqueueAsync(filePath);
            data = _imageThumbnailer.GetThumbnailImageFromMiddleImage(share, path);
            cacheEntryOptions.SetAbsoluteExpiration(TimeSpan.FromMinutes(30));
            // var data = GetThumbnailImageUsingComplicatedFaceDetection(share, path);
            // GetThumbnailImageFromMiddleImageAndPreferImagesWithFaces(share, path);
            await t;

        } else if(!File.Exists(filePath)) {
            throw new Exception($"Cannot choose a thumbnailer for {filePath} because the file does not exist");

        } else if(IsImage(filePath)) {
            data = _imageThumbnailer.GetImageFileThumbnailImage(share, path);
            cacheEntryOptions.SetSlidingExpiration(TimeSpan.FromHours(1));

        } else if(IsVideo(filePath)) {
            data = _videoThumbnailer.GetVideoThumbnail(filePath);
        }


        if(data == null) {
            throw new Exception($"Could not get a thumbnail for share: {share}, path: {path}");
        }

        _cache.Set(cacheKey, data, cacheEntryOptions);
        return data;
    }

    

    private byte[]? GetDirectoryThumbnailImageFromMiddleImageAndPreferImagesWithFaces(string share, string path) {
        return _imageThumbnailer.GetDirectoryThumbnailImageFromMiddleImageAndPreferImagesWithFaces(_shareService.GetPath(share, path));
    }

    private bool IsImage(string path) {
        var extension = Path.GetExtension(path).ToLower();
        switch(extension) {
            case ".jpg":
            case ".jpeg":
            case ".png":
                return true;

            default:
                return false;
        }
    }

    private bool IsVideo(string path) {
        var extension = Path.GetExtension(path).ToLower();
        switch(extension) {
            case ".mp4":
            case ".webm":
                return true;

            default: 
                return false;
        }
    }

    public async Task SetThumbnailCacheAsync(string filePath, byte[] thumbnailData) {
        var cacheKey = $"Thumbnail:Image:{filePath}";
        await _cache.SetAsync(cacheKey, thumbnailData);
    }
}