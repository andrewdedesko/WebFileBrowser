using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebFileBrowser.Services;

namespace WebFileBrowser.Controllers;

[Authorize]
public class AdminController : Controller {
    private readonly ThumbnailPreCacheBackgroundService _thumbnailPreCacheBackgroundService;

    public AdminController(ThumbnailPreCacheBackgroundService thumbnailPreCacheBackgroundService) {
        _thumbnailPreCacheBackgroundService = thumbnailPreCacheBackgroundService;
    }

    public IActionResult Index() {
        var isPreCaching = _thumbnailPreCacheBackgroundService.IsPreCacheRunning();
        ViewData["BackgroundJob:ThumbnailPreCaching"] = isPreCaching;
        string status;
        if(isPreCaching) {
            status = "Pre-caching Thumbnails...";
        } else {
            status = "Idle";
        }

        return View();
    }
}