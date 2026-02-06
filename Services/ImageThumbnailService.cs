using System.Collections;
using Microsoft.Extensions.Caching.Distributed;

namespace WebFileBrowser.Services;

public class ImageThumbnailService : IImageThumbnailService {
    private readonly IShareService _shareService;
    private readonly IBrowseService _browseService;
    private readonly IFileTypeService _fileTypeService;
    private readonly ImageThumbnailer _imageThumbnailer;
    private readonly VideoThumbnailer _videoThumbnailer;
    private readonly IDistributedCache _cache;

    private readonly BackgroundThumbnailQueue _thumbnailQueue;

    public ImageThumbnailService(IShareService shareService, IBrowseService browseService, IFileTypeService fileTypeService, IDistributedCache cache, BackgroundThumbnailQueue thumbnailQueue, ImageThumbnailer imageThumbnailer, VideoThumbnailer videoThumbnailer) {
        _shareService = shareService;
        _browseService = browseService;
        _fileTypeService = fileTypeService;
        _thumbnailQueue = thumbnailQueue;
        _imageThumbnailer = imageThumbnailer;
        _videoThumbnailer = videoThumbnailer;
        _cache = cache;
    }

    public async Task<byte[]> GetImageThumbnail(string share, string path) {
        var cacheKey = $"Thumbnail:Image:webp:{_shareService.GetPath(share, path)}";
        var cachedThumbnail = _cache.Get(cacheKey);
        if(cachedThumbnail != null) {
            return cachedThumbnail;
        }

        DistributedCacheEntryOptions cacheEntryOptions = new();
        var filePath = _shareService.GetPath(share, path);
        byte[]? data = null;
        if(Directory.Exists(filePath)) {
            // data = GetDirectoryThumbnailImageFromMiddleImageAndPreferImagesWithFaces(share, path);
            // var t = _thumbnailQueue.EnqueueAsync(filePath);
            data = _imageThumbnailer.GetThumbnailImageFromMiddleImage(share, path);
            // cacheEntryOptions.SetAbsoluteExpiration(TimeSpan.FromMinutes(30));
            // var data = GetThumbnailImageUsingComplicatedFaceDetection(share, path);
            // GetThumbnailImageFromMiddleImageAndPreferImagesWithFaces(share, path);
            // await t;

        } else if(!File.Exists(filePath)) {
            throw new Exception($"Cannot choose a thumbnailer for {filePath} because the file does not exist");

        } else if(_fileTypeService.IsImage(filePath)) {
            data = _imageThumbnailer.GetImageFileThumbnailImage(share, path);
            cacheEntryOptions.SetSlidingExpiration(TimeSpan.FromHours(1));

        } else if(_fileTypeService.IsVideo(filePath)) {
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

    public async Task SetThumbnailCacheAsync(string filePath, byte[] thumbnailData) {
        var cacheKey = $"Thumbnail:Image:webp:{filePath}";
        await _cache.SetAsync(cacheKey, thumbnailData);
    }
}