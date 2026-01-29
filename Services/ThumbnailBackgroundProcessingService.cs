namespace WebFileBrowser.Services;

public class ThumbnailBackgroundProcessingService : BackgroundService {
    private readonly BackgroundThumbnailQueue _queue;
    private readonly ImageThumbnailer _imageThumbnailer;
    private readonly IImageThumbnailService _imageThumbnailService;
    private readonly ILogger<ThumbnailBackgroundProcessingService> _logger;

    public ThumbnailBackgroundProcessingService(BackgroundThumbnailQueue queue, IImageThumbnailService imageThumbnailService, ImageThumbnailer imageThumbnailer, ILogger<ThumbnailBackgroundProcessingService> logger) {
        _queue = queue;
        _imageThumbnailService = imageThumbnailService;
        _imageThumbnailer = imageThumbnailer;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        while(!stoppingToken.IsCancellationRequested) {
            var path = await _queue.DequeueAsync(stoppingToken);
            _logger.LogInformation($"Processing thumbnail for {path}");
            try {
                if(Directory.Exists(path)) {
                    var data = _imageThumbnailer.GetDirectoryThumbnailImageFromMiddleImageAndPreferImagesWithFaces(path);
                    if(data != null) {
                        await _imageThumbnailService.SetThumbnailCacheAsync(path, data);
                    }
                }
            } catch(Exception e) {
                continue;
            }
        }
    }
}