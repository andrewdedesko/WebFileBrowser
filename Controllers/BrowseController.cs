using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using WebFileBrowser.Models;
using WebFileBrowser.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.RegularExpressions;


namespace WebFileBrowser.Controllers;

[Authorize]
public class BrowseController : Controller
{
    private readonly IShareService _shareService;
    private readonly IBrowseService _browseService;
    private readonly IDistributedCache _cache;

    private readonly IEnumerable<string> _imageExtensions = new List<string>()
    {
        "jpg", "jpeg", "png", "webp", "gif"
    };

    public BrowseController(IShareService shareService, IBrowseService browseService, IDistributedCache cache)
    {
        _shareService = shareService;
        _browseService = browseService;
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
                IsImage = IsImage(Path.GetFileName(f)),
                IsVideo = IsVideo(Path.GetExtension(f).ToLower())
            })
            .OrderBy(f => f.Name)
            .AsEnumerable();

        var directoryContainsImages = files.Any(f => IsImage(f));

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
            if(share == "Andrew" && !string.IsNullOrEmpty(path) && directoryViewModels.Any()){
                var r = new Regex("^Stuff\\/Sites\\/[a-z0-9-_.]+(\\/[a\\a-z0-9-_.]+)?$");
                var m = r.Match(path);
                if (m.Success)
                {
                    viewName = "PreviewIndex";
                }
            }
        } else if (view?.ToLower() == "thumbnails" && directoryViewModels.Any()) {
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
            ShowImageGalleryView = directoryContainsImages
        });
    }

    public IActionResult ViewImages(string share, string path, string image)
    {
        var images = _browseService.GetFiles(share, path)
            .Where(f => IsImage(f))
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
    public IActionResult Thumbnail(string share, string path)
    {
        var cacheKey = $"Thumbnail:Image:${Path.Join(_shareService.GetSharePath(share), path)}";
        var cachedThumbnail = _cache.Get(cacheKey);
        if(cachedThumbnail != null)
        {
            return File(cachedThumbnail, "image/jpeg");
        }

        string? thumbnailFilePath = null;
        var attr = System.IO.File.GetAttributes(Path.Join(_shareService.GetSharePath(share), path));
        if (true || attr.HasFlag(FileAttributes.Directory))
        {
            var dirs = new Queue<string>();
            dirs.Enqueue(Path.Join(_shareService.GetSharePath(share), path));

            while(thumbnailFilePath == null && dirs.Count > 0){
                var currPath = dirs.Dequeue();
                var imageFiles = Directory.GetFiles(currPath)
                .Where(f => IsImage(Path.GetFileName(f)))
                .ToArray();

                if(imageFiles.Any()){
                    thumbnailFilePath = imageFiles[imageFiles.Length / 2];
                }
                else
                {
                    var subDirs = Directory.GetDirectories(currPath);
                    foreach(var d in subDirs)
                    {
                        dirs.Enqueue(d);
                    }
                }
            }
        }
        else
        {
            thumbnailFilePath = Path.Join(_shareService.GetSharePath(share), path);
        }

        if(thumbnailFilePath == null)
        {
            return NotFound();
        }

        using(var image = SixLabors.ImageSharp.Image.Load(thumbnailFilePath))
        {
            var width = 0;
            var height = 0;

            if(image.Width > image.Height)
            {
                width = 240;
            }
            else
            {
                height = 240;
            }
            image.Mutate(x => x.Resize(width, height));

            var thumbnailImageStream = new MemoryStream();
            var writer = new StreamWriter(thumbnailImageStream);
            image.SaveAsJpeg(thumbnailImageStream);

            var thumbnailData = thumbnailImageStream.ToArray();
            _cache.Set(cacheKey, thumbnailData);
            return File(thumbnailData, "image/jpeg");
        }
    }

    private bool IsImage(string filename)
    {
        if (IsMacDotUnderscoreFile(filename))
        {
            return false;
        }

        var extension = Path.GetExtension(filename).ToLower();
        return IsJpgExtension(extension) || IsPngExtension(extension) || IsExtensionWebp(extension) || IsGifExtension(extension);
    }

    private bool IsMacDotUnderscoreFile(string filename) =>
        Path.GetFileName(filename).StartsWith("._");

    private bool IsJpgExtension(string extension) =>
        extension == ".jpg" || extension == ".jpeg";

    private bool IsPngExtension(string extension) =>
        extension == ".png";
    
    private bool IsGifExtension(string extension) =>
        extension == ".gif";
    
    private bool IsExtensionWebp(string extension) =>
        extension == ".webp";

    private bool IsVideo(string extension) => 
        IsMp4(extension) || IsWebm(extension);

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
}