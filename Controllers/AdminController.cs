using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebFileBrowser.Services;

namespace WebFileBrowser.Controllers;

[Authorize]
public class AdminController : Controller {
    // private readonly ThumbnailPreCacheBackgroundService _thumbnailPreCacheBackgroundService;
    private readonly ThumbnailBackgroundProcessingService _thumbnailBackgroundProcessingService;

    public AdminController(ThumbnailBackgroundProcessingService thumbnailBackgroundProcessingService) {
        _thumbnailBackgroundProcessingService = thumbnailBackgroundProcessingService;
    }

    public IActionResult Index() {
        // var isPreCaching = _thumbnailPreCacheBackgroundService?.IsPreCacheRunning() ?? false;
        var isPreCaching = false;
        ViewData["BackgroundJob:ThumbnailPreCaching"] = isPreCaching;
        
        var backgroundThumbnailQueueSize = _thumbnailBackgroundProcessingService.GetQueueCount();
        ViewData["BackgroundJob:ThumbnailQueue:Size"] = backgroundThumbnailQueueSize;

        Response.Headers.Append("Refresh", "5");
        return View();
    }
}