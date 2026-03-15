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
            var t = await _queue.DequeueAsync(stoppingToken);
            _logger.LogTrace($"Processing thumbnail for {t.Share}:{t.Path}");
            try {
                await _imageThumbnailService.GetImageThumbnail(t.Share, t.Path, refreshCache: true, fast: false);
            } catch(Exception ex) {
                _logger.LogError($"Failed to generate thumbnail for {t.Share}:{t.Path}", ex);
                continue;
            }
        }
    }

    public int GetQueueCount() => _queue.Count();
}