using Microsoft.Extensions.Caching.Distributed;
using WebFileBrowser.Models;

namespace WebFileBrowser.Services;

public class ImageThumbnailService : IImageThumbnailService {
    private readonly IShareService _shareService;
    private readonly IBrowseService _browseService;
    private readonly IFileTypeService _fileTypeService;
    private readonly ImageThumbnailer _imageThumbnailer;
    private readonly VideoThumbnailer _videoThumbnailer;
    private readonly IDistributedCache _cache;

    private readonly BackgroundThumbnailQueue _thumbnailQueue;

    private readonly string _thumbnailImageMimeType = "image/webp";

    private readonly int[] _allowedThumbnailCacheSizes = {240, 280, 300, 340};

    public ImageThumbnailService(IShareService shareService, IBrowseService browseService, IFileTypeService fileTypeService, IDistributedCache cache, BackgroundThumbnailQueue thumbnailQueue, ImageThumbnailer imageThumbnailer, VideoThumbnailer videoThumbnailer) {
        _shareService = shareService;
        _browseService = browseService;
        _fileTypeService = fileTypeService;
        _thumbnailQueue = thumbnailQueue;
        _imageThumbnailer = imageThumbnailer;
        _videoThumbnailer = videoThumbnailer;
        _cache = cache;
    }

    public async Task<byte[]> GetImageThumbnail(string share, string path, int size = 240) {
        byte[]? cachedThumbnail = await FindCachedThumbnailAsync(share, path, size);
        if(cachedThumbnail != null) {
            return cachedThumbnail;
        }

        DistributedCacheEntryOptions cacheEntryOptions = new();
        var filePath = _shareService.GetPath(share, path);
        byte[]? data = null;
        if(Directory.Exists(filePath)) {
            // data = GetDirectoryThumbnailImageFromMiddleImageAndPreferImagesWithFaces(share, path);
            // var t = _thumbnailQueue.EnqueueAsync(filePath);
            data = _imageThumbnailer.GetThumbnailImage(share, path, size);
            // cacheEntryOptions.SetAbsoluteExpiration(TimeSpan.FromMinutes(30));
            // var data = GetThumbnailImageUsingComplicatedFaceDetection(share, path);
            // GetThumbnailImageFromMiddleImageAndPreferImagesWithFaces(share, path);
            // await t;

        } else if(!File.Exists(filePath)) {
            throw new Exception($"Cannot choose a thumbnailer for {filePath} because the file does not exist");

        } else if(_fileTypeService.IsImage(filePath)) {
            data = _imageThumbnailer.GetImageFileThumbnailImage(share, path, size);
            cacheEntryOptions.SetSlidingExpiration(TimeSpan.FromHours(1));

        } else if(_fileTypeService.IsVideo(filePath)) {
            data = _videoThumbnailer.GetVideoThumbnail(filePath, size);
        }


        if(data == null) {
            throw new ThumbnailNotAvailableException($"Could not get a thumbnail for share: {share}, path: {path}");
        }

        await SetThumbnailCacheAsync(share, path, size, data);
        return data;
    }

    private async Task<byte[]?> FindCachedThumbnailAsync(string share, string path, int size) {
        var cacheKey = _thumbnailCacheKey(share, path, size);
        var cachedThumbnail = await _cache.GetAsync(cacheKey);
        if(cachedThumbnail != null) {
            return cachedThumbnail;
        }

        // Migrate old thumbnail cache to new cache with sizes
        if(size == 240){
            var old240PxCacheKey = _oldThumbnail240PxCacheKey(share, path);
            var oldCached240PxThumbnail = _cache.Get(old240PxCacheKey);
            if(oldCached240PxThumbnail != null) {
                await _cache.RemoveAsync(old240PxCacheKey);
                await SetThumbnailCacheAsync(share, path, size, oldCached240PxThumbnail);
                return oldCached240PxThumbnail;
            }
        }

        return null;
    }

    public async Task FlushThumbnailFromCache(string share, string path) {
        foreach(var size in _allowedThumbnailCacheSizes){
            await _cache.RemoveAsync(_thumbnailCacheKey(share, path, size));
        }
    }

    public string GetThumbnailImageMimeType() =>
        _thumbnailImageMimeType;

    private byte[]? GetDirectoryThumbnailImageFromMiddleImageAndPreferImagesWithFaces(string share, string path, int size) {
        return _imageThumbnailer.GetDirectoryThumbnailImageFromMiddleImageAndPreferImagesWithFaces(_shareService.GetPath(share, path), size);
    }

    public Task SetThumbnailCacheAsync(string share, string path, int size, byte[] thumbnailData) =>
        SetThumbnailCacheAsync(_shareService.GetPath(share, path), size, thumbnailData);

    public async Task SetThumbnailCacheAsync(string filePath, int size, byte[] thumbnailData) {
        if(_allowedThumbnailCacheSizes.Contains(size)){
            await _cache.SetAsync(_thumbnailCacheKey(filePath, size), thumbnailData);
        }
    }

    private string _thumbnailCacheKey(string share, string path, int size) =>
        _thumbnailCacheKey(_shareService.GetPath(share, path), size);
    
    private string _thumbnailCacheKey(string path, int size){
        return $"Thumbnail:Image:{_thumbnailImageMimeType}:{size}:{path}";
    }

    private string _oldThumbnail240PxCacheKey(string share, string path) {
        return $"Thumbnail:Image:{_thumbnailImageMimeType}:{_shareService.GetPath(share, path)}";
    }

    private void _validateThumbnailSize(int size) {
        // if(!_allowedThumbnailSizes.Contains(size)) {
        //     throw new ArgumentException($"Invalid thumbnail size: {size}");
        // }
    }
}