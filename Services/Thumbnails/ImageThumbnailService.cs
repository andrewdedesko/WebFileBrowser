using System.Text.Json;
using System.Text.Json.Serialization;
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
        byte[]? cachedThumbnail = await FindCachedThumbnailAsync(share, path, size);
        if(useCache && !refreshCache) {
            if(cachedThumbnail != null) {
                return cachedThumbnail;
            }
        }

        var absoluteExpiration = DateTime.Now + TimeSpan.FromDays(45);
        var freshnessExpiration = TimeSpan.FromDays(30);
        var filePath = _shareService.GetPath(share, path);
        byte[]? data = null;

        if(Directory.Exists(filePath)) {
            if(fast) {
                var thumbnailImage = _directoryThumbnailer.FindThumbnail(share, path);
                if(thumbnailImage != null) {
                    _imageThumbnailer.ScaleImageToThumbnail(thumbnailImage, size);
                    data = _imageThumbnailer.GetImageAsBytes(thumbnailImage);
                }

                await _backgroundThumbnailQueue.EnqueueAsync(new ThumbnailTask() {
                    Share = share,
                    Path = path
                });

            } else {
                IEnumerable<Tuple<string, string, CropResult>> thumbnailOptions = _directoryThumbnailer.FindBestThumbnailImage(share, path);

                if(thumbnailOptions.Any()) {
                    var bestThumbnailOption = thumbnailOptions.OrderByDescending(o => o.Item3.Score).First();
                    var bestThumbnailPath = bestThumbnailOption.Item1;
                    var bestThumbnailHash = bestThumbnailOption.Item2;
                    var bestThumbnailCropResult = bestThumbnailOption.Item3;

                    var bestThumbnailMetadata = new ThumbnailMetadata() {
                        ImagePath = bestThumbnailPath,
                        ImageFileHash = bestThumbnailHash,
                        ExpiresAt = absoluteExpiration,
                        Crop = new ThumbnailCropMetadata() {
                            CropScore = bestThumbnailCropResult.Score,
                            CropRectangleLeft = bestThumbnailCropResult.Box.Left,
                            CropRectangleTop = bestThumbnailCropResult.Box.Top,
                            CropRectangleRight = bestThumbnailCropResult.Box.Right,
                            CropRectangleBottom = bestThumbnailCropResult.Box.Bottom
                        }
                    };

                    var thumbnailMetadataCacheKey = $"Thumbnail:Metadata:{_thumbnailImageMimeType}:{_shareService.GetPath(share, path)}";

                    ThumbnailMetadata? cachedThumbnailMetadata = null;
                    var cachedThumbnailMetadataStr = await _cache.GetStringAsync(thumbnailMetadataCacheKey);
                    if(!string.IsNullOrEmpty(cachedThumbnailMetadataStr)) {
                        // _logger.LogInformation("{metadata}", cachedThumbnailMetadataStr);
                        try{
                            cachedThumbnailMetadata = JsonSerializer.Deserialize<ThumbnailMetadata>(cachedThumbnailMetadataStr);
                        } catch(Exception) {
                        }
                    }

                    if(cachedThumbnailMetadata != null && _thumbnailMetadataAreEqual(cachedThumbnailMetadata, bestThumbnailMetadata)) {
                        // _logger.LogInformation("Thumbnail {share}:{path} is already up to date", share, path);
                        data = cachedThumbnail;

                        cachedThumbnailMetadata.ExpiresAt = absoluteExpiration;
                        await _cache.SetStringAsync(thumbnailMetadataCacheKey, JsonSerializer.Serialize(cachedThumbnailMetadata));
                    } else{
                        using(var image = Image.Load<Rgb24>(_shareService.GetPath(share, bestThumbnailPath))) {
                            image.Mutate(i => i.Crop(bestThumbnailCropResult.Box));
                            _imageThumbnailer.ScaleImageToThumbnail(image, size);
                            data = _imageThumbnailer.GetImageAsBytes(image);
                        }

                        await _cache.SetStringAsync(thumbnailMetadataCacheKey, JsonSerializer.Serialize(bestThumbnailMetadata));
                    }
                }
            }

        } else if(!File.Exists(filePath)) {
            throw new Exception($"Cannot choose a thumbnailer for {filePath} because the file does not exist");

        } else if(_fileTypeService.IsImage(filePath)) {
            data = _imageThumbnailer.GetImageFileThumbnailImage(share, path, size);

        } else if(_fileTypeService.IsVideo(filePath)) {
            data = _videoThumbnailer.GetVideoThumbnail(filePath, size);
        }

        if(data == null) {
            throw new ThumbnailNotAvailableException($"Could not get a thumbnail for share: {share}, path: {path}");
        }

        if(useCache || refreshCache) {
            await _cacheThumbnailAsync(share, path, size, data, absoluteExpiration);
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

    private async Task _cacheThumbnailAsync(string share, string path, int size, byte[] thumbnailData, DateTime absoluteExpiration) {
        if(_allowedThumbnailCacheSizes.Contains(size)) {
            DistributedCacheEntryOptions cacheEntryOptions = new();
            cacheEntryOptions.SetAbsoluteExpiration(absoluteExpiration);
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

    private string _thumbnailFreshnessCacheKey(string path) {
        return $"Thumbnail:Freshness:{_thumbnailImageMimeType}:{path}";
    }

    private string _oldThumbnail240PxCacheKey(string share, string path) {
        return $"Thumbnail:Image:{_thumbnailImageMimeType}:{_shareService.GetPath(share, path)}";
    }

    private static bool _thumbnailMetadataAreEqual(ThumbnailMetadata a, ThumbnailMetadata b) {
        if(a == null || b == null) {
            return false;
        }

        if(a.ImagePath != b.ImagePath) {
            return false;
        }

        if(a.ImageFileHash != b.ImageFileHash) {
            return false;
        }

        if(a.Crop == null && b.Crop == null) {
            return true;
        }

        if(a.Crop == null || b.Crop == null) {
            return false;
        }

        if(a.Crop.CropRectangleLeft != b.Crop.CropRectangleLeft) {
            return false;
        }

        if(a.Crop.CropRectangleTop != b.Crop.CropRectangleTop) {
            return false;
        }

        if(a.Crop.CropRectangleRight != b.Crop.CropRectangleRight) {
            return false;
        }

        if(a.Crop.CropRectangleBottom != b.Crop.CropRectangleBottom) {
            return false;
        }

        return true;
    }

    private class ThumbnailMetadata {
        [JsonPropertyName("imagePath")]
        public required string ImagePath {get; set;}

        [JsonPropertyName("imageFileHash")]
        public required string ImageFileHash {get; set;}

        [JsonPropertyName("crop")]
        public ThumbnailCropMetadata? Crop { get; set; }

        [JsonPropertyName("expiresAt")]
        public required DateTime ExpiresAt {get; set;}
    }

    private class ThumbnailCropMetadata {
        [JsonPropertyName("rectangleLeft")]
        public int CropRectangleLeft { get; set; }

        [JsonPropertyName("rectangleTop")]
        public int CropRectangleTop { get; set; }

        [JsonPropertyName("rectangleRight")]
        public int CropRectangleRight { get; set; }

        [JsonPropertyName("rectangleBottom")]
        public int CropRectangleBottom { get; set; }

        [JsonPropertyName("score")]
        public double? CropScore { get; set; }
    }
}