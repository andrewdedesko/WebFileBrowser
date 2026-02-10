using Microsoft.AspNetCore.Components.Forms;
using WebFileBrowser.Configuration;

namespace WebFileBrowser.Services;

public class ThumbnailPreCacheBackgroundService : BackgroundService {
    private readonly IShareService _shareService;
    private readonly IBrowseService _browseService;
    private readonly DefaultViews _defaultViews;
    private readonly BackgroundThumbnailQueue _backgroundThumbnailQueue;
    private readonly ILogger<ThumbnailPreCacheBackgroundService> _logger;

    public ThumbnailPreCacheBackgroundService(BackgroundThumbnailQueue backgroundThumbnailQueue, DefaultViews defaultViews, IShareService shareService, IBrowseService browseService, ILogger<ThumbnailPreCacheBackgroundService> logger) {
        _backgroundThumbnailQueue = backgroundThumbnailQueue;
        _defaultViews = defaultViews;
        _shareService = shareService;
        _browseService = browseService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        foreach(var share in _shareService.GetShareNames()) {
            _logger.LogInformation($"Pre-caching thumbnails for share {share}");

            try{
                await PreCacheShareThumbnails(stoppingToken, share);
            }catch(Exception ex) {
                _logger.LogError($"An error occurred while pre caching thumbnails for share {share}", ex);
            }

            if(stoppingToken.IsCancellationRequested) {
                break;
            }
        }
    }

    private async Task PreCacheShareThumbnails(CancellationToken cancellationToken, string share) {
        Queue<string> directories = new();
        directories.Enqueue("");

        while(!cancellationToken.IsCancellationRequested && directories.Any()) {
            var dir = directories.Dequeue();
            if(PathMatchesThumbnailViewPatterns(dir)) {
                await _backgroundThumbnailQueue.EnqueueAsync(new Models.ThumbnailTask() {
                    Share = share,
                    Path = dir
                });
            }

            var dirs = _browseService.GetDirectories(share, dir);
            foreach(var d in dirs) {
                directories.Enqueue(d);
            }
        }
    }

    private bool PathMatchesThumbnailViewPatterns(string path) {
        if(string.IsNullOrEmpty(path)) {
            return false;
        }

        foreach(var p in _defaultViews.ThumbnailViewPathPatterns) {
            if(p.Match(path).Success) {
                return true;
            }
        }

        return false;
    }
}