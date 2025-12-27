using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using WebFileBrowser.Models;
using WebFileBrowser.Services;

namespace WebFileBrowser.Controllers;

public class BrowseController : Controller
{
    private readonly IShareService _shareService;

    private readonly IEnumerable<string> _imageExtensions = new List<string>()
    {
        "jpg", "jpeg", "png", "webp", "gif"
    };

    public BrowseController(IShareService shareService)
    {
        _shareService = shareService;
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

    public IActionResult Index(string share, string path)
    {
        var dirPath = Path.Join(_shareService.GetSharePath(share), path);
        var directories = System.IO.Directory.GetDirectories(dirPath);

        var directoryViewModels = directories
            .Select(d => new DirectoryViewModel()
            {
                Name = Path.GetFileName(d),
                Share = share,
                Path = Path.GetRelativePath(_shareService.GetSharePath(share), d)
            })
            .OrderBy(d => d.Name)
            .AsEnumerable();

        var files = System.IO.Directory.GetFiles(dirPath);
        var fileViewModels = files
            .Select(f => new FileViewModel()
            {
                Name = Path.GetFileName(f),
                Share = share,
                Path = Path.GetRelativePath(_shareService.GetSharePath(share), f)
            })
            .OrderBy(f => f.Name)
            .AsEnumerable();

        var directoryContainsImages = files.Any(f => IsImage(f));

        IList<PathViewModel> pathComponents = new List<PathViewModel>();
        var shareDir = new DirectoryInfo(_shareService.GetSharePath(share));
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

        return View(new BrowseDirectoryViewModel()
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

    public IActionResult ViewImages(string share, string path)
    {
        var dirPath = Path.Join(_shareService.GetSharePath(share), path);
        var images = System.IO.Directory.GetFiles(dirPath)
            .Where(f => IsImage(f))
            .Select(f => new FileViewModel()
            {
                Name = Path.GetFileName(f),
                Share = share,
                Path = Path.GetRelativePath(_shareService.GetSharePath(share), f)
            })
            .OrderBy(f => f.Name)
            .AsEnumerable();

        return View(new BrowseDirectoryViewModel()
        {
            Name = Path.GetFileName(path),
            Share = share,
            Path = Path.GetRelativePath(_shareService.GetSharePath(share), dirPath),
            Directories = Enumerable.Empty<DirectoryViewModel>(),
            Files = images
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
}