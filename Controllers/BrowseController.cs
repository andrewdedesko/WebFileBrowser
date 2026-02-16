using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using WebFileBrowser.Models;
using WebFileBrowser.Services;
using SixLabors.ImageSharp;
using Microsoft.Extensions.Caching.Distributed;
using WebFileBrowser.Configuration;


namespace WebFileBrowser.Controllers;

[Authorize]
public class BrowseController : Controller
{
    private readonly IShareService _shareService;
    private readonly IBrowseService _browseService;
    private readonly IFileTypeService _fileTypeService;
    private readonly IImageThumbnailService _imageThumbnailService;
    private readonly DefaultViews _defaultViews;
    private readonly IDistributedCache _cache;

    public BrowseController(IShareService shareService, IBrowseService browseService, DefaultViews defaultViews, IImageThumbnailService imageThumbnailService, IDistributedCache cache, IFileTypeService fileTypeService) {
        _shareService = shareService;
        _browseService = browseService;
        _fileTypeService = fileTypeService;
        _defaultViews = defaultViews;
        _imageThumbnailService = imageThumbnailService;
        _cache = cache;
    }

    public IActionResult Shares()
    {
        var shares = _shareService.GetShareNames();
        var directoryViewModels = shares
            .Select(s => new DirectoryViewModel()
            {
                Name = s,
                Share = s
            })
            .OrderBy(d => d.Name)
            .AsEnumerable();

            return View(new BrowseDirectoryViewModel()
        {
            Name = "Shares",
            Share = null,
            Path = null,
            PathComponents = Enumerable.Empty<PathViewModel>(),
            Directories = directoryViewModels,
            Files = Enumerable.Empty<FileViewModel>(),
            ShowImageGalleryView = false
        });
    }

    public IActionResult Index(string share, string path, string? view)
    {
        var directories = _browseService.GetDirectories(share, path);

        var directoryViewModels = directories
            .Select(d => new DirectoryViewModel()
            {
                Name = Path.GetFileName(d),
                Share = share,
                Path = d,
                ViewType = view
            })
            .OrderBy(d => d.Name)
            .AsEnumerable();

        var files = _browseService.GetFiles(share, path);
        var fileViewModels = files
            .Select(f => new FileViewModel()
            {
                Name = Path.GetFileName(f),
                Share = share,
                Path = f,
                IsImage = _fileTypeService.IsImage(Path.GetFileName(f)),
                IsVideo = _fileTypeService.IsVideo(Path.GetExtension(f).ToLower())
            })
            .OrderBy(f => f.Name)
            .AsEnumerable();

        var directoryContainsImages = files.Any(f => _fileTypeService.IsImage(f));

        IList<PathViewModel> pathComponents = new List<PathViewModel>();
        var shareDir = new DirectoryInfo(_shareService.GetSharePath(share));
        var dirPath = Path.Join(_shareService.GetSharePath(share), path);
        var dir = new DirectoryInfo(dirPath);
        while(dir != null && dir.FullName != shareDir.FullName)
        {
            var p = new PathViewModel()
            {
                Name = dir.Name,
                Path = Path.GetRelativePath(shareDir.FullName, dir.FullName)
            };
            pathComponents.Insert(0, p);
            dir = dir.Parent;
        }
        pathComponents.Insert(0, new PathViewModel()
        {
            Name = share,
            Path = null
        });

        var viewName = "Index";
        if (string.IsNullOrEmpty(view)) {
            if(!string.IsNullOrEmpty(path) && directories.Any()){
                if (PathMatchesThumbnailViewPatterns(path)){
                    viewName = "PreviewIndex";
                }
            }
        } else if (view?.ToLower() == "thumbnails") {
            viewName = "PreviewIndex";
        }
        return View(viewName, new BrowseDirectoryViewModel()
        {
            Name = Path.GetFileName(dirPath),
            Share = share,
            Path = Path.GetRelativePath(_shareService.GetSharePath(share), dirPath),
            PathComponents = pathComponents,
            Directories = directoryViewModels,
            Files = fileViewModels,
            ViewType = viewName == "PreviewIndex" ? "Thumbnails" : "List",
            ShowImageGalleryView = directoryContainsImages
        });
    }

    public IActionResult ViewImages(string share, string path, string image)
    {
        var images = _browseService.GetFiles(share, path)
            .Where(f => _fileTypeService.IsImage(f))
            .Select(f => new FileViewModel()
            {
                Name = Path.GetFileName(f),
                Share = share,
                Path = f
            })
            .OrderBy(f => f.Name)
            .AsEnumerable();

        string? startingImageName = null;
        if (!string.IsNullOrEmpty(image))
        {
            startingImageName = image;
        }
        return View(new ViewImagesViewModel()
        {
            Name = Path.GetFileName(path),
            Share = share,
            Path = path,
            Files = images,
            StartingImageName = startingImageName
        });
    }

    [ResponseCache(CacheProfileName = "Media")]
    public IActionResult Image(string share, string path)
    {
        var imagePath = Path.Join(_shareService.GetSharePath(share), path);
        
        
        var image = System.IO.File.OpenRead(imagePath);
        string mimeType;
        if (!new FileExtensionContentTypeProvider().TryGetContentType(imagePath, out mimeType))
        {
            throw new Exception("Unsupported type");
        }

        return File(image, mimeType);
    }

    public IActionResult ViewVideo(string share, string path)
    {
        var viewModel = new ViewVideoViewModel()
        {
            Share = share,
            Path = path,
            Name = Path.GetFileName(path),
            VideoMimeType = GetVideoMimeType(path)
        };

        return View(viewModel);
    }

    public IActionResult Video(string share, string path)
    {
        var videoPath = Path.Join(_shareService.GetSharePath(share), path);
        var filestream = System.IO.File.OpenRead(videoPath);
        return File(filestream, GetVideoMimeType(path), fileDownloadName: Path.GetFileName(videoPath), enableRangeProcessing: true);
    }

    [ResponseCache(CacheProfileName = "Media")]
    public async Task<IActionResult> Thumbnail(string share, string path, int size = 240)
    {
        try{
            var thumbnail = await _imageThumbnailService.GetImageThumbnail(share, path, size);
            return File(thumbnail, _imageThumbnailService.GetThumbnailImageMimeType());
        } catch(ThumbnailNotAvailableException) {
            return NotFound();
        }
    }

    public async Task<IActionResult> FlushThumbnail(string share, string path) {
        await _imageThumbnailService.FlushThumbnailFromCache(share, path);
        return Ok();
    }

    private bool IsMp4(string extension) =>
        extension == ".mp4";

    private bool IsWebm(string extension) =>
        extension == ".webm";
    
    private string GetVideoMimeType(string path)
    {
        string videoExtension = Path.GetExtension(path).ToLower();

        if (IsMp4(videoExtension))
        {
            return "video/mp4";
        } else if (IsWebm(videoExtension))
        {
            return "video/webm";
        }

        throw new Exception("Unsupported video extension");
    }

    private bool PathMatchesThumbnailViewPatterns(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return false;
        }

        foreach(var p in _defaultViews.ThumbnailViewPathPatterns)
        {
            if (p.Match(path).Success)
            {
                return true;
            }
        }

        return false;
    }
}