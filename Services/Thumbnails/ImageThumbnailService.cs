using Microsoft.Extensions.Caching.Distributed;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using WebFileBrowser.Models;

namespace WebFileBrowser.Services;

public class ImageThumbnailService : IImageThumbnailService {
    private readonly IShareService _shareService;
    private readonly IBrowseService _browseService;
    private readonly IFileTypeService _fileTypeService;
    private readonly ImageThumbnailer _imageThumbnailer;
    private readonly VideoThumbnailer _videoThumbnailer;
    private readonly DirectoryThumbnailer _directoryThumbnailer;
    private readonly ThumbnailAutoCropper _thumbnailAutoCropper;
    private readonly IDistributedCache _cache;
    private readonly BackgroundThumbnailQueue _backgroundThumbnailQueue;
    private readonly ILogger<ImageThumbnailService> _logger;

    private readonly string _thumbnailImageMimeType = "image/webp";

    private readonly int[] _allowedThumbnailCacheSizes = { 240, 280, 300, 340 };

    public ImageThumbnailService(IShareService shareService, IBrowseService browseService, IFileTypeService fileTypeService, IDistributedCache cache, ImageThumbnailer imageThumbnailer, VideoThumbnailer videoThumbnailer, ILogger<ImageThumbnailService> logger, DirectoryThumbnailer directoryThumbnailer, BackgroundThumbnailQueue backgroundThumbnailQueue, ThumbnailAutoCropper thumbnailAutoCropper) {
        _shareService = shareService;
        _browseService = browseService;
        _fileTypeService = fileTypeService;
        _imageThumbnailer = imageThumbnailer;
        _videoThumbnailer = videoThumbnailer;
        _directoryThumbnailer = directoryThumbnailer;
        _thumbnailAutoCropper = thumbnailAutoCropper;
        _cache = cache;
        _backgroundThumbnailQueue = backgroundThumbnailQueue;
        _logger = logger;
    }

    public async Task<byte[]> GetImageThumbnail(string share, string path, int size, bool useCache = true, bool refreshCache = false, bool fast = true) {
        if(useCache && !refreshCache) {
            byte[]? cachedThumbnail = await FindCachedThumbnailAsync(share, path, size);
            if(cachedThumbnail != null) {
                return cachedThumbnail;
            }
        }

        DistributedCacheEntryOptions cacheEntryOptions = new();
        var absoluteExpiration = TimeSpan.FromDays(Random.Shared.Next(20, 25));
        var filePath = _shareService.GetPath(share, path);
        byte[]? data = null;
        if(Directory.Exists(filePath)) {
            if(fast) {
                var thumbnailImage = _directoryThumbnailer.FindThumbnail(share, path);
                if(thumbnailImage != null) {
                    cacheEntryOptions.SetAbsoluteExpiration(TimeSpan.FromHours(1));
                    await _backgroundThumbnailQueue.EnqueueAsync(new ThumbnailTask() {
                        Share = share,
                        Path = path
                    });

                    _imageThumbnailer.ScaleImageToThumbnail(thumbnailImage, size);
                    data = _imageThumbnailer.GetImageAsBytes(thumbnailImage);
                }

            } else {
                IEnumerable<Tuple<string, CropResult>> thumbnailOptions = _directoryThumbnailer.FindBestThumbnailImage(share, path);

                if(thumbnailOptions.Any()) {
                    var bestThumbnailOption = thumbnailOptions.OrderByDescending(o => o.Item2.Score).First();
                    var bestThumbnailPath = bestThumbnailOption.Item1;
                    using(var image = Image.Load<Rgb24>(_shareService.GetPath(share, bestThumbnailPath))) {
                        image.Mutate(i => i.Crop(bestThumbnailOption.Item2.Box));
                        _imageThumbnailer.ScaleImageToThumbnail(image, size);
                        data = _imageThumbnailer.GetImageAsBytes(image);
                    }
                }
            }

        } else if(!File.Exists(filePath)) {
            throw new Exception($"Cannot choose a thumbnailer for {filePath} because the file does not exist");

        } else if(_fileTypeService.IsImage(filePath)) {
            data = _imageThumbnailer.GetImageFileThumbnailImage(share, path, size);
            cacheEntryOptions.SetSlidingExpiration(TimeSpan.FromDays(1));

        } else if(_fileTypeService.IsVideo(filePath)) {
            data = _videoThumbnailer.GetVideoThumbnail(filePath, size);
        }

        if(data == null) {
            throw new ThumbnailNotAvailableException($"Could not get a thumbnail for share: {share}, path: {path}");
        }

        if(useCache || refreshCache) {
            await SetThumbnailCacheAsync(share, path, size, data, absoluteExpiration);
        }

        return data;
    }

    private async Task<byte[]?> FindCachedThumbnailAsync(string share, string path, int size) {
        var cacheKey = _thumbnailCacheKey(share, path, size);
        var cachedThumbnail = await _cache.GetAsync(cacheKey);
        if(cachedThumbnail != null) {
            return cachedThumbnail;
        }

        return null;
    }

    public async Task FlushThumbnailFromCache(string share, string path) {
        foreach(var size in _allowedThumbnailCacheSizes) {
            await _cache.RemoveAsync(_thumbnailCacheKey(share, path, size));
        }
    }

    public async Task FlushThumbnailFromCacheRecursiveAsync(string share, string path) {
        Queue<string> paths = new();
        paths.Enqueue(path);

        while(paths.Any()) {
            var p = paths.Dequeue();
            var files = _browseService.GetFiles(share, p);
            foreach(var f in files) {
                await FlushThumbnailFromCache(share, f);
            }

            var directories = _browseService.GetDirectories(share, p);
            foreach(var d in directories) {
                paths.Enqueue(d);
                await FlushThumbnailFromCache(share, d);
            }
        }
    }

    public string GetThumbnailImageMimeType() =>
        _thumbnailImageMimeType;

    private async Task SetThumbnailCacheAsync(string share, string path, int size, byte[] thumbnailData, TimeSpan absoluteExpiry) {
        if(_allowedThumbnailCacheSizes.Contains(size)) {
            DistributedCacheEntryOptions cacheEntryOptions = new();
            cacheEntryOptions.SetAbsoluteExpiration(absoluteExpiry);
            await _cache.SetAsync(_thumbnailCacheKey(_shareService.GetPath(share, path), size), thumbnailData, cacheEntryOptions);
        }
    }

    public async Task RefreshThumbnailAsync(string share, string path, int size) {
        await FlushThumbnailFromCache(share, path);
        // await GetImageThumbnail(share, path, size, refreshCache: true);
        await _backgroundThumbnailQueue.EnqueueAsync(new ThumbnailTask() {
            Share = share,
            Path = path
        });
    }

    public async Task RefreshThumbnailsAsync(string share, string path, int size) {
        Queue<string> paths = new();
        paths.Enqueue(path);

        // await GetImageThumbnail(share, path, size, refreshCache: true);

        while(paths.Any()) {
            var currentPath = paths.Dequeue();
            // _logger.LogInformation("Queueing {share}:{path} for thumbnail refresh", share, currentPath);
            await _backgroundThumbnailQueue.EnqueueAsync(new ThumbnailTask() {
                Share = share,
                Path = currentPath
            });

            if(_browseService.IsDirectory(share, currentPath)) {
                var directories = _browseService.GetDirectories(share, currentPath);
                foreach(var directory in directories) {
                    paths.Enqueue(directory);
                }

                // var files = _browseService.GetFiles(share, path);
                // foreach(var file in files) {
                //     paths.Enqueue(file);
                // }
            }
        }
    }

    private string _thumbnailCacheKey(string share, string path, int size) =>
        _thumbnailCacheKey(_shareService.GetPath(share, path), size);

    private string _thumbnailCacheKey(string path, int size) {
        return $"Thumbnail:Image:{_thumbnailImageMimeType}:{size}:{path}";
    }

    private string _oldThumbnail240PxCacheKey(string share, string path) {
        return $"Thumbnail:Image:{_thumbnailImageMimeType}:{_shareService.GetPath(share, path)}";
    }
}